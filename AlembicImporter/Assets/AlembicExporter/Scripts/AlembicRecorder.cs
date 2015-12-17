using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public abstract class AlembicCustomRecorder : MonoBehaviour
{
    public abstract void SetParent(aeAPI.aeObject parent);
    public abstract void Capture();
}



[ExecuteInEditMode]
class AlembicRecorder : MonoBehaviour
{
    #region impl

    public static void CaptureTransform(aeAPI.aeXForm abc, Transform trans)
    {
        aeAPI.aeXFormSampleData data;
        data.inherits = false;
        data.translation = trans.position;
        data.scale = trans.lossyScale;
        trans.rotation.ToAngleAxis(out data.rotation_angle, out data.rotation_axis);
        aeAPI.aeXFormWriteSample(abc, ref data);
    }

    public static void CaptureCamera(aeAPI.aeCamera abc, Camera cam)
    {
        // todo
    }

    public static void CaptureMesh(aeAPI.aePolyMesh abc, Mesh mesh)
    {
        aeAPI.aePolyMeshSampleData data = new aeAPI.aePolyMeshSampleData();
        var vertices = mesh.vertices;
        var indices = mesh.GetIndices(0); // todo: record all submeshes
        data.positions = Marshal.UnsafeAddrOfPinnedArrayElement(vertices, 0);
        data.indices = Marshal.UnsafeAddrOfPinnedArrayElement(indices, 0);
        data.vertex_count = vertices.Length;
        data.index_count = indices.Length;

        aeAPI.aePolyMeshWriteSample(abc, ref data);
    }


    public abstract class Recorder
    {
        public abstract void Capture();
    }

    public class TransformRecorder : Recorder
    {
        Transform m_target;
        aeAPI.aeXForm m_abc;

        public TransformRecorder(Transform target, aeAPI.aeXForm abc)
        {
            m_target = target;
            m_abc = abc;
        }

        public override void Capture()
        {
            if (m_target != null)
            {
                CaptureTransform(m_abc, m_target);
            }
        }
    }

    public class CameraRecorder : Recorder
    {
        Camera m_target;
        aeAPI.aeCamera m_abc;

        public CameraRecorder(Camera target, aeAPI.aeCamera abc)
        {
            m_target = target;
            m_abc = abc;
        }

        public override void Capture()
        {
            if (m_target != null)
            {
                CaptureCamera(m_abc, m_target);
            }
        }
    }

    public class MeshRecorder : Recorder
    {
        MeshRenderer m_target;
        aeAPI.aePolyMesh m_abc;

        public MeshRecorder(MeshRenderer target, aeAPI.aePolyMesh abc)
        {
            m_target = target;
            m_abc = abc;
        }

        public override void Capture()
        {
            if(m_target != null)
            {
                CaptureMesh(m_abc, m_target.GetComponent<MeshFilter>().sharedMesh);
            }
        }
    }

    public class SkinnedMeshRecorder : Recorder
    {
        SkinnedMeshRenderer m_target;
        aeAPI.aePolyMesh m_abc;

        public SkinnedMeshRecorder(SkinnedMeshRenderer target, aeAPI.aePolyMesh abc)
        {
            m_target = target;
            m_abc = abc;
        }

        public override void Capture()
        {
            if (m_target != null)
            {
                CaptureMesh(m_abc, m_target.sharedMesh);
            }
        }
    }

    public class CustomRecorderHandler : Recorder
    {
        AlembicCustomRecorder m_target;

        public CustomRecorderHandler(AlembicCustomRecorder target)
        {
            m_target = target;
        }

        public override void Capture()
        {
            if (m_target != null)
            {
                m_target.Capture();
            }
        }
    }

    #endregion



    public string m_path;
    public aeAPI.aeConfig m_conf = aeAPI.aeConfig.default_value;
    public bool m_showHud;

    public bool m_captureMeshRenderer = true;
    public bool m_captureSkinnedMeshRenderer = true;
    public bool m_captureCamera = true;
    public bool m_customRecorder = true;

    aeAPI.aeContext m_ctx;
    List<Recorder> m_recorders = new List<Recorder>();
    bool m_recording;
    float m_time;


    public bool BeginRecording()
    {
        if(m_recording) { return true; }

        m_ctx = aeAPI.aeCreateContext(ref m_conf);
        if(m_ctx.ptr == IntPtr.Zero) {
            Debug.Log("aeCreateContext() failed");
            return false;
        }
        if(!aeAPI.aeOpenArchive(m_ctx, m_path))
        {
            Debug.Log("aeOpenArchive() failed");
            aeAPI.aeDestroyContext(m_ctx);
            m_ctx = new aeAPI.aeContext();
            return false;
        }


        var top = aeAPI.aeGetTopObject(m_ctx);

        if (m_captureCamera)
        {
            foreach(var target in FindObjectsOfType<Camera>())
            {
                var trans_obj = aeAPI.aeCreateObject(top, target.name + "_trans");
                var trans_abc = aeAPI.aeAddXForm(trans_obj);
                var trans_rec = new TransformRecorder(target.GetComponent<Transform>(), trans_abc);
                m_recorders.Add(trans_rec);

                var cam_obj = aeAPI.aeCreateObject(trans_obj, target.name);
                var cam_abc = aeAPI.aeAddCamera(cam_obj);
                var cam_rec = new CameraRecorder(target, cam_abc);
                m_recorders.Add(cam_rec);
            }
        }

        if (m_captureMeshRenderer)
        {
            foreach (var target in FindObjectsOfType<MeshRenderer>())
            {
                var trans_obj = aeAPI.aeCreateObject(top, target.name + "_trans");
                var trans_abc = aeAPI.aeAddXForm(trans_obj);
                var trans_rec = new TransformRecorder(target.GetComponent<Transform>(), trans_abc);
                m_recorders.Add(trans_rec);

                var mesh_obj = aeAPI.aeCreateObject(trans_obj, target.name);
                var mesh_abc = aeAPI.aeAddPolyMesh(mesh_obj);
                var mesh_rec = new MeshRecorder(target, mesh_abc);
                m_recorders.Add(mesh_rec);
            }
        }

        if (m_captureSkinnedMeshRenderer)
        {
            foreach (var target in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                var trans_obj = aeAPI.aeCreateObject(top, target.name + "_trans");
                var trans_abc = aeAPI.aeAddXForm(trans_obj);
                var trans_rec = new TransformRecorder(target.GetComponent<Transform>(), trans_abc);
                m_recorders.Add(trans_rec);

                var mesh_obj = aeAPI.aeCreateObject(trans_obj, target.name);
                var mesh_abc = aeAPI.aeAddPolyMesh(mesh_obj);
                var mesh_rec = new SkinnedMeshRecorder(target, mesh_abc);
                m_recorders.Add(mesh_rec);
            }
        }

        if (m_customRecorder)
        {
            foreach (var target in FindObjectsOfType<AlembicCustomRecorder>())
            {
                target.SetParent(top);
                var mesh_rec = new CustomRecorderHandler(target);
                m_recorders.Add(mesh_rec);
            }
        }

        m_recording = true;
        m_time = 0.0f;
        return true;
    }

    public void EndRecording()
    {
        if (!m_recording) { return; }
        aeAPI.aeDestroyContext(m_ctx);
        m_ctx = new aeAPI.aeContext();
    }


    IEnumerator ProcessRecording()
    {
        yield return new WaitForEndOfFrame();

        aeAPI.aeSetTime(m_ctx, m_time);
        foreach(var recorder in m_recorders) {
            recorder.Capture();
        }
    }


    void Update()
    {
        if(m_recording)
        {
            m_time += Time.deltaTime;
            StartCoroutine(ProcessRecording());
        }
    }
}
