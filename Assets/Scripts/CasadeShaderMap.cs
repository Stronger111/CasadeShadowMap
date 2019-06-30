using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CasadeShaderMap : MonoBehaviour
{
    /// <summary>
    /// 方向光
    /// </summary>
    public Light dirLight;
    /// <summary>
    /// 灯光方向的摄像机
    /// </summary>
    Camera dirLightCamera;
    /// <summary>
    /// 阴影分辨率
    /// </summary>
    public int shadowResolution = 1;
    public Shader shadowCaster = null;

    private Matrix4x4 biasMatrix = Matrix4x4.identity;
    /// <summary>
    /// 世界转换到阴影贴图的矩阵
    /// </summary>
    List<Matrix4x4> worldToShadowMatrix = new List<Matrix4x4>(4);
    /// <summary>
    /// 
    /// </summary>
    GameObject[] dirLightCameraSplits = new GameObject[4];
    /// <summary>
    /// 深度图
    /// </summary>
    RenderTexture[] depthTextures = new RenderTexture[4];

    private void OnDestroy()
    {
        dirLightCamera = null;
        for (int i = 0; i < depthTextures.Length; i++)
        {
            if (depthTextures[i])
            {
                DestroyImmediate(depthTextures[i]);
            }
        }
    }
    private void Awake()
    {
        biasMatrix.SetRow(0, new Vector4(0.5f, 0, 0, 0.5f));
        biasMatrix.SetRow(1, new Vector4(0, 0.5f, 0, 0.5f));
        biasMatrix.SetRow(2, new Vector4(0, 0, 0.5f, 0.5f));
        biasMatrix.SetRow(3, new Vector4(0, 0, 0, 1f));
        InitFrustumCorners();
    }
    private void Update()
    {
        CalcMainCameraSplitsFrustumCorners();
        CalcLightCameraSplitsFrustum();

        if (dirLight)
        {
            if (!dirLightCamera)
            {
                dirLightCamera = CreateLightCamera();
                CreateRenderTexture();
            }
            Shader.SetGlobalFloat("_gShadowBias", 0.005f);
            Shader.SetGlobalFloat("_gShadowStrength", 0.5f);

            worldToShadowMatrix.Clear();

            for (int i = 0; i < 4; i++)
            {
                ConstructLightCameraSplits(i);
                dirLightCamera.targetTexture = depthTextures[i];
                dirLightCamera.RenderWithShader(shadowCaster,"");

                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(dirLightCamera.projectionMatrix,false);

                worldToShadowMatrix.Add(projectionMatrix*dirLightCamera.worldToCameraMatrix);
            }
            Shader.SetGlobalMatrixArray("_gWorld2Shadow", worldToShadowMatrix);
        }
    }

    void ConstructLightCameraSplits(int k)
    {
        dirLightCamera.transform.position = dirLightCameraSplits[k].transform.position;
        dirLightCamera.transform.rotation = dirLightCameraSplits[k].transform.rotation;

        dirLightCamera.nearClipPlane = lightCamera_Splits_fcs[k].nearCorners[0].z;
        dirLightCamera.farClipPlane = lightCamera_Splits_fcs[k].farCorners[0].z;
        dirLightCamera.aspect = Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[0] - lightCamera_Splits_fcs[k].nearCorners[1]) / Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[1] - lightCamera_Splits_fcs[k].nearCorners[2]);
        dirLightCamera.orthographicSize = Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[1] - lightCamera_Splits_fcs[k].nearCorners[2]) * 0.5f;

    }
    /// <summary>
    /// 创建灯光摄像机
    /// </summary>
    /// <returns></returns>
    private Camera CreateLightCamera()
    {
        GameObject goLightCamera = new GameObject("Directional Light Camera");
        Camera LightCamera = goLightCamera.AddComponent<Camera>();
        LightCamera.cullingMask = 1 << LayerMask.NameToLayer("Caster");
        LightCamera.backgroundColor = Color.white;
        LightCamera.clearFlags = CameraClearFlags.SolidColor;
        LightCamera.orthographic = true;
        LightCamera.enabled = false;

        for (int i = 0; i < 4; i++)
        {
            dirLightCameraSplits[i] = new GameObject("dirLightCameraSplits" + i);
        }

        return LightCamera;
    }
    private void CreateRenderTexture()
    {
        //rt格式565
        RenderTextureFormat rtFormat = RenderTextureFormat.Default;
        if (!SystemInfo.SupportsRenderTextureFormat(rtFormat))
            rtFormat = RenderTextureFormat.Default;
        for (int i = 0; i < 4; i++)
        {
            depthTextures[i] = new RenderTexture(1024, 1024, 24, rtFormat);
            Shader.SetGlobalTexture("_gShadowMapTexture" + i, depthTextures[i]);
        }
    }
    struct FrustumCorners
    {
        public Vector3[] nearCorners;
        public Vector3[] farCorners;
    }
    FrustumCorners[] mainCamera_Splits_fcs;
    FrustumCorners[] lightCamera_Splits_fcs;
    /// <summary>
    /// 初始化视锥顶点
    /// </summary>
    void InitFrustumCorners()
    {
        mainCamera_Splits_fcs = new FrustumCorners[4];
        lightCamera_Splits_fcs = new FrustumCorners[4];
        for (int i = 0; i < mainCamera_Splits_fcs.Length; i++)
        {
            mainCamera_Splits_fcs[i].nearCorners = new Vector3[4];
            mainCamera_Splits_fcs[i].farCorners = new Vector3[4];

            lightCamera_Splits_fcs[i].farCorners = new Vector3[4];
            lightCamera_Splits_fcs[i].nearCorners = new Vector3[4];
        }
    }
    /// <summary>
    /// 灯光分割距离近平面
    /// </summary>
    float[] _LightSplitsNear;
    float[] _LightSplitsFar;
    void CalcMainCameraSplitsFrustumCorners()
    {
        float near = Camera.main.nearClipPlane;
        float far = Camera.main.farClipPlane;
        //分割近平面
        float[] nears = { near, far * 0.067f + near, far * 0.133f + far * 0.067f + near, far * 0.267f + far * 0.133f + far * 0.067f + near };
        float[] fars = { far * 0.067f + near, far * 0.133f + far * 0.067f + near, far * 0.267f + far * 0.133f + far * 0.067f + near, far };

        _LightSplitsNear = nears;
        _LightSplitsFar = fars;

        Shader.SetGlobalVector("_gLightSplitsNear", new Vector4(_LightSplitsNear[0], _LightSplitsNear[1], _LightSplitsNear[2], _LightSplitsNear[3]));
        Shader.SetGlobalVector("_gLightSplitsFar", new Vector4(_LightSplitsFar[0], _LightSplitsFar[1], _LightSplitsFar[2], _LightSplitsFar[3]));

        for (int i = 0; i < 4; i++)
        {
            Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), _LightSplitsNear[i], Camera.MonoOrStereoscopicEye.Mono, mainCamera_Splits_fcs[i].nearCorners);
            for (int k = 0; k < 4; k++)
            {
                //转换到世界空间中
                mainCamera_Splits_fcs[i].nearCorners[k] = Camera.main.transform.TransformPoint(mainCamera_Splits_fcs[i].nearCorners[k]);
            }
            //获取摄像机视锥体本地坐标
            Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), _LightSplitsFar[i], Camera.MonoOrStereoscopicEye.Mono, mainCamera_Splits_fcs[i].farCorners);
            for (int k = 0; k < 4; k++)
            {
                //转换到世界空间中
                mainCamera_Splits_fcs[i].farCorners[k] = Camera.main.transform.TransformPoint(mainCamera_Splits_fcs[i].farCorners[k]);
            }
        }
    }
    void CalcLightCameraSplitsFrustum()
    {
        if (dirLightCamera == null)
            return;
        for (int k = 0; k < 4; k++)
        {
            for (int i = 0; i < 4; i++)
            {
                //摄像机视锥顶点近平面转换到灯光的本地空间中
                lightCamera_Splits_fcs[k].nearCorners[i] = dirLightCameraSplits[k].transform.InverseTransformPoint(mainCamera_Splits_fcs[k].nearCorners[i]);
                lightCamera_Splits_fcs[k].farCorners[i] = dirLightCameraSplits[k].transform.InverseTransformPoint(mainCamera_Splits_fcs[k].farCorners[i]);

            }
            //x
            float[] xs = { lightCamera_Splits_fcs[k].nearCorners[0].x, lightCamera_Splits_fcs[k].nearCorners[1].x, lightCamera_Splits_fcs[k].nearCorners[2].x, lightCamera_Splits_fcs[k].nearCorners[3].x,
                       lightCamera_Splits_fcs[k].farCorners[0].x, lightCamera_Splits_fcs[k].farCorners[1].x, lightCamera_Splits_fcs[k].farCorners[2].x, lightCamera_Splits_fcs[k].farCorners[3].x };
            //y
            float[] ys = { lightCamera_Splits_fcs[k].nearCorners[0].y, lightCamera_Splits_fcs[k].nearCorners[1].y, lightCamera_Splits_fcs[k].nearCorners[2].y, lightCamera_Splits_fcs[k].nearCorners[3].y,
                       lightCamera_Splits_fcs[k].farCorners[0].y, lightCamera_Splits_fcs[k].farCorners[1].y, lightCamera_Splits_fcs[k].farCorners[2].y, lightCamera_Splits_fcs[k].farCorners[3].y };
            //z
            float[] zs = { lightCamera_Splits_fcs[k].nearCorners[0].z, lightCamera_Splits_fcs[k].nearCorners[1].z, lightCamera_Splits_fcs[k].nearCorners[2].z, lightCamera_Splits_fcs[k].nearCorners[3].z,
                       lightCamera_Splits_fcs[k].farCorners[0].z, lightCamera_Splits_fcs[k].farCorners[1].z, lightCamera_Splits_fcs[k].farCorners[2].z, lightCamera_Splits_fcs[k].farCorners[3].z };
            //灯光分割空间的最小值X
            float minX = Mathf.Min(xs);
            float maxX = Mathf.Max(xs);

            float minY = Mathf.Min(ys);
            float maxY = Mathf.Max(ys);

            float minZ = Mathf.Min(zs);
            float maxZ = Mathf.Max(zs);

            lightCamera_Splits_fcs[k].nearCorners[0] = new Vector3(minX, minY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[1] = new Vector3(maxX, minY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[2] = new Vector3(maxX, maxY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[3] = new Vector3(minX, maxY, minZ);

            lightCamera_Splits_fcs[k].farCorners[0] = new Vector3(minX, minY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[1] = new Vector3(maxX, minY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[2] = new Vector3(maxX, maxY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[3] = new Vector3(minX, maxY, maxZ);

            Vector3 pos = lightCamera_Splits_fcs[k].nearCorners[0] + (lightCamera_Splits_fcs[k].nearCorners[2] - lightCamera_Splits_fcs[k].nearCorners[0]) * 0.5f;
            //转换到世界空间中
            dirLightCameraSplits[k].transform.position = dirLightCameraSplits[k].transform.TransformPoint(pos);
            dirLightCameraSplits[k].transform.rotation = dirLight.transform.rotation;
        }
    }

}
