﻿using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public abstract class RenderingApproach
{
    virtual public void Prepare(GameObject model) { }
    virtual public void SetEnabled(bool enabled) { }
    virtual public void OnRenderObject(Camera cam = null, Transform root = null) { }
    virtual public void LateUpdate(Camera cam = null, Transform root = null) { }
    virtual public void Dispose() { }
    virtual public void OnGUI() { }
}

// Use the basic Unity Renderer component
public class RendererTest : RenderingApproach
{
    GameObject go;

    public override void Prepare(GameObject model)
    {
        Dictionary<Color, Material> matDict = new Dictionary<Color, Material>();
        Shader shader = Shader.Find("Basic Shader");
        go = Object.Instantiate(model);

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            Color32 col = r.sharedMaterial.color;
            if (!matDict.ContainsKey(col))
            {
                Material mat = new Material(shader);
                mat.color = col;

                matDict[col] = mat;
            }

            r.sharedMaterial = matDict[col];
        }
    }

    public override void SetEnabled(bool state)
    {
        go.SetActive(state);
    }

    public override void Dispose()
    {
        Object.Destroy(go);
    }
}

// Use the Graphics.DrawMesh() API
public class DrawMeshTest : RenderingApproach
{
    struct DrawSet
    {
        public Material material;
        public Mesh mesh;
        public Matrix4x4 localMat;
    }

    DrawSet[] _drawArray;

    public override void Prepare(GameObject model)
    {
        List<DrawSet> drawList = new List<DrawSet>();
        Dictionary<Color, Material> matDict = new Dictionary<Color, Material>();
        Shader shader = Shader.Find("Basic Shader");

        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            Transform transform = r.transform;

            Color col = r.sharedMaterial.color;
            if (!matDict.ContainsKey(col))
            {
                Material mat = new Material(shader);
                mat.color = col;
                matDict.Add(col, mat);
            }

            drawList.Add(new DrawSet()
            {
                material = matDict[col],
                mesh = mesh,
                localMat = transform.localToWorldMatrix
            });
        }

        _drawArray = drawList.ToArray();
    }

    public override void LateUpdate(Camera cam = null, Transform root = null)
    {
        for(int i = 0; i < _drawArray.Length; i++)
        {
            DrawSet ds = _drawArray[i];
            Graphics.DrawMesh(ds.mesh, root.localToWorldMatrix * ds.localMat, ds.material, 0);
        }
    }
}

// Use the Graphics.DrawMesh() API with MaterialPropertyBlocks
public class DrawMeshWithPropBlockTest : RenderingApproach
{
    struct DrawSet
    {
        public MaterialPropertyBlock propBlock;
        public Mesh mesh;
        public Matrix4x4 localMat;
    }

    DrawSet[] _drawArray;
    Material _mat;

    public override void Prepare(GameObject model)
    {
        List<DrawSet> drawList = new List<DrawSet>();
        Shader shader = Shader.Find("Basic Shader Per Renderer");
        _mat = new Material(shader);

        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            Transform transform = r.transform;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", r.sharedMaterial.color);

            drawList.Add(new DrawSet()
            {
                propBlock = mpb,
                mesh = mesh,
                localMat = transform.localToWorldMatrix
            });
        }

        _drawArray = drawList.ToArray();
    }

    public override void LateUpdate(Camera cam = null, Transform root = null)
    {
        Debug.Log(cam);
        for (int i = 0; i < _drawArray.Length; i++)
        {
            DrawSet ds = _drawArray[i];
            Graphics.DrawMesh(ds.mesh, root.localToWorldMatrix * ds.localMat, _mat, 0, cam, 0, ds.propBlock);
        }
    }
}

// Use the Graphics.DrawProcedural API with separate
// buffers for both the indices and attributes
public class DrawProceduralTest : RenderingApproach
{
    struct DrawSet
    {
        public Material material;
        public ComputeBuffer idxsBuffer;
        public ComputeBuffer attrBuffer;
        public Matrix4x4 localMat;
        public int count;
    }

    DrawSet[] drawArray;

    protected virtual void GetBuffers(Mesh mesh, ref ComputeBuffer idxbuff, ref ComputeBuffer attrbuff, ref int count)
    {
        ImportStructuredBufferMesh.Import(mesh, ref idxbuff, ref attrbuff);
        count = idxbuff.count;
    }

    protected virtual Shader GetShader() {
        return Shader.Find("Indirect Shader");
    }

    public override void Prepare(GameObject model)
    {
        List<DrawSet> drawList = new List<DrawSet>();
        Shader shader = GetShader();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf.sharedMesh;
            ComputeBuffer idxbuff = null;
            ComputeBuffer attrbuff = null;
            int count = 0;

            GetBuffers(mesh, ref idxbuff, ref attrbuff, ref count);

            Material mat = new Material(shader);
            mat.SetBuffer("indices", idxbuff);
            mat.SetBuffer("points", attrbuff);
            mat.color = r.sharedMaterial.color;

            drawList.Add(new DrawSet()
            {
                material = mat,
                idxsBuffer = idxbuff,
                attrBuffer = attrbuff,
                count = count,
                localMat = r.transform.localToWorldMatrix
            });
        }

        drawArray = drawList.ToArray();
    }

    public override void OnRenderObject(Camera cam = null, Transform root = null)
    {
        for (int i = 0; i < drawArray.Length; i++)
        {
            DrawSet ds = drawArray[i];
            GL.PushMatrix();
            GL.MultMatrix(root.localToWorldMatrix * ds.localMat);
            ds.material.SetPass(0);
            Graphics.DrawProcedural(MeshTopology.Triangles, ds.count, 1);
            GL.PopMatrix();
        }
    }

    public override void Dispose()
    {
        for (int i = 0; i < drawArray.Length; i++)
        {
            if(drawArray[i].idxsBuffer != null) drawArray[i].idxsBuffer.Dispose();
            if(drawArray[i].attrBuffer != null) drawArray[i].attrBuffer.Dispose();
        }
    }
}

// Use the Graphics.DrawProcedural API with separate
// buffers for both the indices and attributes
public class UnpackedDrawProceduralTest : DrawProceduralTest
{
    protected override void GetBuffers(Mesh mesh, ref ComputeBuffer idxbuff, ref ComputeBuffer attrbuff, ref int count)
    {
        idxbuff = null;
        ImportStructuredBufferMesh.ImportAndUnpack(mesh, ref attrbuff);
        count = attrbuff.count;
    }

    protected override Shader GetShader()
    {
        return Shader.Find("Unpacked Indirect Shader");
    }
}

// Use the Graphics.DrawProcedural API with separate
// buffers for both the indices and attributes
public class VisibleTriangleRenderTest : RenderingApproach
{
    struct OtherAttrs
    {
        public Matrix4x4 matrix;
        public Color color;
    }

    // Render constants
    const int OC_RESOLUTION = 1024;     // OC Texture Resolution
    const int OC_RENDER_FRAMES = 5;     // Number of frames to render the OC over
    const float OC_FOV_RATIO = 1.25f;    // FOV ratio for the OC render camera
    const int MAX_TRIANGLES = 350000;   // Number of triangles to render per frame

    // Compute Shader Kernels
    const int ACCUM_KERNEL = 0;
    const int MAP_KERNEL = 1;

    RenderTexture octex;
    Camera occam;

    ComputeShader compute;
    ComputeBuffer idaccum, triappend;

    ComputeBuffer attrbuff, otherbuff;
    Material mat;
    Material idmat;

    Coroutine ocRoutine;

    Transform root;

    public override void Prepare(GameObject model)
    {
        // OC camera and rendertexture
        octex = new RenderTexture(OC_RESOLUTION, OC_RESOLUTION, 16, RenderTextureFormat.ARGB32);
        octex.enableRandomWrite = true;
        octex.Create();
        occam = new GameObject("OC CAM").AddComponent<Camera>();
        occam.targetTexture = octex;
        occam.enabled = false;
        
        // Collect the mesh and attribute buffers
        List<Mesh> meshes = new List<Mesh>();
        List<OtherAttrs> otherattrs = new List<OtherAttrs>();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            meshes.Add(mf.sharedMesh);
            otherattrs.Add(new OtherAttrs(){
                matrix = r.transform.localToWorldMatrix,
                color = r.sharedMaterial.color
            });
        }

        // Triangle buffers
        ImportStructuredBufferMesh.ImportAllAndUnpack(meshes.ToArray(), ref attrbuff);
        otherbuff = new ComputeBuffer(otherattrs.Count, Marshal.SizeOf(typeof(OtherAttrs)), ComputeBufferType.Default);
        otherbuff.SetData(otherattrs.ToArray());

        // Compute Shader Buffers
        idaccum = new ComputeBuffer(attrbuff.count / 3, Marshal.SizeOf(typeof(bool)));
        triappend = new ComputeBuffer(MAX_TRIANGLES, Marshal.SizeOf(typeof(uint)), ComputeBufferType.Append);

        // Compute Shader & Buffers
        compute = Resources.Load<ComputeShader>("Shaders/compute/countTris");
        
        compute.SetBuffer(ACCUM_KERNEL, "_idaccum", idaccum);
        compute.SetTexture(ACCUM_KERNEL, "_idTex", octex);

        compute.SetBuffer(MAP_KERNEL, "_idaccum", idaccum);
        compute.SetBuffer(MAP_KERNEL, "_triappend", triappend);
        
        // Material
        mat = new Material(Shader.Find("Indirect Shader Single Call"));
        mat.SetBuffer("other", otherbuff);
        mat.SetBuffer("points", attrbuff);
        mat.SetBuffer("triappend", triappend);

        idmat = new Material(Shader.Find("Indirect Shader Single Call Ids"));
        idmat.SetBuffer("other", otherbuff);
        idmat.SetBuffer("points", attrbuff);
    }

    public override void SetEnabled(bool enabled)
    {
        if (enabled) ocRoutine = StaticCoroutine.Start(GatherTriangles());
        else StaticCoroutine.Stop(ocRoutine);
    }

    IEnumerator GatherTriangles()
    {
        // TODO: Dispatch multiple threads to better use
        // compute shader
        // TODO: On particularly large models iterating over
        // the texture is simpler than iterating over
        // every triangle id 1024^2 = 1048576, so models
        // will less geometry are more expensive to iterate over
        // The data may be more expensive to transfer, though
        // TODO: Could use a triangle idex to save on attribute buffer memory
        // TODO: Could use predictive positioning with the camera
        while (true)
        {
            while (this.root == null) yield return null;

            Transform root = this.root;
            
            // Render the OC frame
            occam.CopyFrom(Camera.main);
            occam.fieldOfView *= OC_FOV_RATIO;
            occam.targetTexture = octex;

            // Render over several frames
            int totaltris = attrbuff.count / 3;
            int trisperframe = totaltris / OC_RENDER_FRAMES;
            for (int i = 0; i < totaltris; i += trisperframe)
            {
                // Set the oc texture to active and clear it
                // with an id-color that doesn't clash with a
                // triangle id
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = octex;
                if (i == 0)
                {
                    GL.Clear(true, true, new Color32(0xFF, 0xFF, 0xFF, 0xFF));
                }

                // Draw the set number of triangles
                GL.PushMatrix();
                GL.LoadIdentity();
                GL.modelview = occam.worldToCameraMatrix;
                GL.LoadProjectionMatrix(occam.projectionMatrix);
                GL.MultMatrix(root.localToWorldMatrix);
                idmat.SetInt("idOffset", i * 3);
                idmat.SetPass(0);
                Graphics.DrawProcedural(MeshTopology.Triangles, trisperframe * 3, 1);

                GL.PopMatrix();
                RenderTexture.active = prev;

                if ( i + trisperframe < totaltris) yield return null;
            }

            // NOTE
            // It's possible that this yield may
            // not be necessary -- the compute shaders
            // seem to run fairly quickly and
            // can just be run after the previous draw
            yield return null;

            // accumulate the ids
            triappend.SetCounterValue(0);
            compute.Dispatch(ACCUM_KERNEL, octex.width, octex.height, 1);
            compute.Dispatch(MAP_KERNEL, idaccum.count, 1, 1);

            yield return null;
        }
    }
    
    public override void OnRenderObject(Camera cam = null, Transform root = null)
    {
        this.root = root;
        if (Camera.main != cam) return;

        GL.PushMatrix();
        GL.MultMatrix(root.localToWorldMatrix);
        mat.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, MAX_TRIANGLES * 3, 1);
        GL.PopMatrix();
    }

    public override void Dispose()
    {
        Object.Destroy(octex);
        attrbuff.Dispose();
        otherbuff.Dispose();
        triappend.Dispose();

        triappend.Dispose();
        idaccum.Dispose();
    }
}