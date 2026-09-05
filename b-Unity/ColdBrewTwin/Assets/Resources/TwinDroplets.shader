Shader "ColdBrewTwin/Droplets"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct vertex { float4 position:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct pixel { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            pixel vert(vertex v) { pixel o; o.position=UnityObjectToClipPos(v.position); o.color=v.color; o.uv=v.uv; return o; }
            fixed4 frag(pixel i):SV_Target
            {
                float2 p=i.uv*2-1; float radius=dot(p,p); clip(1-radius);
                return fixed4(i.color.rgb+pow(saturate(1-length(p-float2(-.25,.3))),8)*.3,i.color.a*saturate((1-radius)*3));
            }
            ENDCG
        }
    }
}
