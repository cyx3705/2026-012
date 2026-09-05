Shader "ColdBrewTwin/Studio Glass"
{
    Properties
    {
        _Color ("Tint",Color) = (.3,.72,.94,.10)
        _EdgeOpacity ("Edge opacity",Range(0,1)) = .32
        _Glossiness ("Smoothness",Range(0,1)) = .96
        _Frost ("Frost",Range(0,1)) = 0
        _Section ("Section",Float) = 0
        _CutNormal ("Cut normal",Vector) = (0,0,-1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" }
        Cull Back
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:premul
        #pragma target 3.0
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        fixed4 _Color;
        float _EdgeOpacity,_Section,_Glossiness,_Frost;
        float4 _CutNormal;
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            if(_Section>.5) clip(-dot(IN.worldPos.xz,_CutNormal.xz));
            float3 normal=WorldNormalVector(IN,float3(0,0,1));
            float3 view=_WorldSpaceCameraPos-IN.worldPos;
            float facing=abs(dot(normal,view))*rsqrt(max(dot(normal,normal)*dot(view,view),1e-12));
            float fresnel=pow(1-saturate(facing),3);
            o.Albedo=_Color.rgb;
            float grain=sin(IN.worldPos.x*7000+IN.worldPos.z*5300)*cos(IN.worldPos.y*6700-IN.worldPos.z*4100);
            o.Normal=normalize(float3(grain*.07*_Frost,grain*.045*_Frost,1));
            o.Metallic=.05;
            o.Smoothness=_Glossiness;
            o.Alpha=saturate(_Color.a+fresnel*_EdgeOpacity);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
