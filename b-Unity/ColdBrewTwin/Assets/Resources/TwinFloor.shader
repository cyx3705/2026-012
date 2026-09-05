Shader "ColdBrewTwin/Studio Floor"
{
    Properties
    {
        _Color ("Floor",Color) = (.55,.59,.61,1)
        _Horizon ("Horizon",Color) = (.83,.86,.87,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows noforwardadd finalcolor:horizon
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color,_Horizon;
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            o.Albedo=_Color.rgb; o.Smoothness=.30; o.Metallic=.05; o.Alpha=1;
        }
        void horizon(Input IN,SurfaceOutputStandard o,inout fixed4 color)
        {
            float fade=smoothstep(1.1,2.8,distance(_WorldSpaceCameraPos,IN.worldPos));
            color.rgb=lerp(color.rgb,_Horizon.rgb,fade);
        }
        ENDCG
    }
    FallBack "Standard"
}
