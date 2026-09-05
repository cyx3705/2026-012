Shader "ColdBrewTwin/Channel Section"
{
    Properties
    {
        _Color ("Coffee",Color) = (.32,.11,.035,1)
        _Ceiling ("Liquid front",Float) = 0
        _Travel ("Flow travel",Float) = 0
        _Moving ("Moving",Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+20" "RenderType"="Opaque" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Ceiling,_Travel,_Moving;
            struct Vertex {float4 vertex:POSITION;};
            struct Pixel {float4 position:SV_POSITION; float height:TEXCOORD0;};
            Pixel vert(Vertex v)
            {
                Pixel o; o.position=UnityObjectToClipPos(v.vertex);
                o.height=mul(unity_ObjectToWorld,v.vertex).y; return o;
            }
            fixed4 frag(Pixel i):SV_Target
            {
                clip(_Ceiling-i.height);
                return fixed4(_Color.rgb*(.92+.08*_Moving*sin(i.height*180-_Travel*420)),1);
            }
            ENDCG
        }
    }
}
