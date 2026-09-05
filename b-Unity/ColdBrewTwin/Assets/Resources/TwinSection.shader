Shader "ColdBrewTwin/Section"
{
    Properties
    {
        _Color ("Color", Color) = (0.72,0.76,0.76,1)
        _Metallic ("Metallic", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.55
        _Section ("Section", Float) = 0
        _CutNormal ("Cut Normal", Vector) = (0,0,-1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color;
        half _Metallic, _Glossiness;
        float _Section;
        float4 _CutNormal;
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            if (_Section > 0.5) clip(-dot(IN.worldPos.xz, _CutNormal.xz));
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
