Shader "ColdBrewTwin/Flowing Water"
{
    Properties
    {
        _Color ("Water",Color) = (0.04,0.43,0.53,0.76)
        _Clock ("Simulation time",Float) = 0
        _Travel ("Flow travel",Float) = 0
        _Amplitude ("Ripple height",Float) = 0
        _Radius ("Surface radius",Float) = 0.063
        _SurfaceY ("Surface height",Float) = 0
        _Ceiling ("Fill front",Float) = 10
        _IsChannel ("Annular channel",Float) = 0
        _Section ("Section",Float) = 0
        _CutNormal ("Cut normal",Vector) = (0,0,-1,0)
        _Moving ("Moving",Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        float _Clock,_Travel,_Amplitude,_Radius,_SurfaceY,_Ceiling,_IsChannel,_Section,_Moving;
        float4 _CutNormal;
        struct Input { float3 worldPos; float3 viewDir; };
        void vert(inout appdata_full v)
        {
            float edge = saturate(1-length(v.vertex.xz)/max(_Radius,0.001));
            float surface = v.normal.y>0.8 && abs(v.vertex.y-_SurfaceY)<0.0001 ? 1:0;
            float wave = sin(length(v.vertex.xz-float2(.028,0))*320-_Clock*9)*.55
                       + sin(v.vertex.x*220+v.vertex.z*170+_Clock*7)*.45;
            v.vertex.y += _Amplitude*wave*edge*surface;
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(_Ceiling-IN.worldPos.y);
            if (_IsChannel>0.5 && _Section>0.5)
                clip(-dot(IN.worldPos.xz,_CutNormal.xz));
            float ripple = sin(IN.worldPos.x*230+IN.worldPos.z*170-_Clock*6);
            float cross = cos(IN.worldPos.z*270-IN.worldPos.x*100+_Clock*4);
            float run = sin(IN.worldPos.y*430-_Travel*650);
            float sheen = pow(saturate(ripple*cross*.5+.5),9);
            o.Normal = normalize(float3(ripple*.065,cross*.065,1));
            o.Albedo = _Color.rgb + sheen*float3(.045,.08,.10) + _Moving*run*.015;
            o.Emission = _Color.rgb*.04 + sheen*.01;
            o.Metallic = .05;
            o.Smoothness = .94;
            float fresnel = pow(1-saturate(dot(normalize(IN.viewDir),o.Normal)),3);
            o.Alpha = saturate(_Color.a + fresnel*lerp(.2,.07,_IsChannel));
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
