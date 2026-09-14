// Porte para URP do `VHS_Mobile.shader` do pacote F2F_VhsFree, que esta ao lado
// dele em `Assets/ThirdParty/F2F_VhsFree/` e ficou por tocar.
//
// **A matematica e a mesma, linha a linha.** Fisheye com cantos pretos, separacao
// de canais em X, ruido de baixa resolucao a deslizar. O aspecto e o do pacote e
// os numeros sao os que vieram no material — o porte nao e uma reinterpretacao.
//
// Tres coisas mudaram, e nenhuma delas se ve:
//
// 1. **CG passou a HLSL e `_MainTex` passou a `_BlitTexture`.** O original era um
//    efeito de imagem legado, desenhado para o `OnRenderImage` do pipeline
//    integrado — que **nunca e chamado em URP**. Nao dava erro: dava nada.
// 2. **O tempo vem de fora, em `_TimeSeconds`.** Era o `_Time.y`, e trocar por um
//    valor dado pelo C# poupa uma dependencia de ordem de includes e, sobretudo,
//    deixa o ruido continuar a andar com o tempo do jogo parado. No dia em que
//    houver menu de pausa, uma imagem completamente congelada por tras dele
//    denuncia que aquilo nao e video, e uma fita parada continua a chiar.
// 3. **`_Intensity` multiplica tudo.** O §12 do plano pede reducao e desactivacao
//    do VHS, e um efeito que so tem "ligado" nao a consegue dar. A zero, isto e
//    uma copia exacta da imagem.
Shader "Pungent/VHS (URP)"
{
    Properties
    {
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _BleedAmount("Bleed Amount", Float) = 0.002
        _NoiseAmount("Noise Amount", Float) = 0.025
        _FisheyeBend("Fisheye Bend", Float) = 0.1
        _TimeSpeed("Time Speed", Float) = 3.0
        _TimeSeconds("Time (dado pelo C#)", Float) = 0.0
        _Intensity("Intensity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Pungent VHS"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            // O `Core.hlsl` do URP **antes** do `Blit.hlsl` do core, e nao ao
            // contrario: o segundo declara `TEXTURE2D_X(_BlitTexture)` a espera de
            // que as macros de textura da plataforma ja existam, e quem as traz e o
            // primeiro. Trocada a ordem, isto nao compila.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            float _BleedAmount;
            float _NoiseAmount;
            float _FisheyeBend;
            float _TimeSpeed;
            float _TimeSeconds;
            float _Intensity;
            float4 _PhoneClarityRect;
            float _PhoneClarity;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.texcoord;
                float2 uv = screenUV;

                // Mascara suave sobre o ecra diegetico do telemovel. Nao tenta
                // inverter o VHS depois de ele destruir informacao: impede apenas
                // o bleed e a maior parte do ruido de a destruirem nesta zona. A
                // curvatura global continua igual, por isso nao aparece uma bolha
                // limpa ou uma quebra visivel no filtro.
                float2 clarityCenter = (_PhoneClarityRect.xy + _PhoneClarityRect.zw) * 0.5;
                float2 clarityHalf = max((_PhoneClarityRect.zw - _PhoneClarityRect.xy) * 0.5,
                    float2(0.00001, 0.00001));
                float2 clarityDistance = abs(screenUV - clarityCenter) / clarityHalf;
                float clarityEdge = max(clarityDistance.x, clarityDistance.y);
                float clarityMask = (1.0 - smoothstep(0.78, 1.0, clarityEdge))
                    * saturate(_PhoneClarity);

                // A intensidade entra aqui e nao no fim. Misturar a imagem limpa com
                // a imagem torta dava fantasma duplo — duas copias da cena
                // sobrepostas com deslocamentos diferentes, que e o que se ve num
                // ecra estragado e nao numa cassete. Encolher os proprios efeitos
                // devolve, a zero, exactamente a imagem original.
                float bleed = _BleedAmount * _Intensity * lerp(1.0, 0.08, clarityMask);
                float bend = _FisheyeBend * _Intensity;
                float grain = _NoiseAmount * _Intensity * lerp(1.0, 0.25, clarityMask);

                // Fisheye com cantos pretos.
                float2 centered = uv - 0.5;
                float len = length(centered);
                uv = 0.5 + centered * (1.0 + bend * len * len);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0.0, 0.0, 0.0, 1.0);

                // Separacao de canais. So em X, como no original: o desalinhamento
                // horizontal e o da fita; o vertical le-se como 3D mal calibrado.
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(bleed, 0.0)).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(bleed, 0.0)).b;
                half4 col = half4(r, g, b, 1.0);

                // Ruido a um quarto da resolucao, a deslizar. O deslize e nos dois
                // eixos ao mesmo tempo, tal como no original.
                float2 noiseUV = uv * 0.25 + _TimeSeconds * _TimeSpeed;
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                // **O grao entra em espaco de visualizacao, e nao no buffer linear.**
                //
                // O original corria no `OnRenderImage`, depois do tonemapping, sobre
                // valores ja prontos para o ecra: somar 0,0125 ali e um cocegas.
                // Aqui o passe corre antes do pos-processamento, onde a imagem esta
                // em linear HDR — e um cinzento que no ecra vale 0,2 vale 0,033 em
                // linear. A mesma soma passa a mexer 38% do valor, e como o
                // tonemapping esmaga mais em baixo do que levanta em cima, o
                // resultado nao e ruido: e a imagem inteira 17% mais escura. Medido,
                // com o fisheye a zero para a geometria nao mentir: 0,2045 -> 0,1705.
                //
                // Converter, somar, e voltar poe o numero do autor a fazer o que o
                // autor queria que fizesse.
                float3 display = LinearToSRGB(max(col.rgb, 0.0));
                display += (n - 0.5) * grain;
                col.rgb = SRGBToLinear(max(display, 0.0));

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
