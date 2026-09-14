using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Pungent.Atmosphere;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o VHS a correr, em todos os niveis de qualidade.
    ///
    /// O pacote F2F_VhsFree traz o efeito como `OnRenderImage` num componente da
    /// camara, e **esse metodo nunca e chamado em URP**: sem erro, sem aviso, sem
    /// imagem. Arrastar o componente para a camara e a coisa obvia a fazer e nao faz
    /// nada — que e exactamente a categoria de falha que este projecto ja pagou seis
    /// vezes. O <see cref="VhsRendererFeature"/> substitui-o; esta ferramenta liga-o.
    ///
    /// **Nos tres renderers e nao so no activo.** O projecto tem tres niveis de
    /// qualidade — Performant, Balanced, High Fidelity — cada um com o seu renderer.
    /// Ligar so o que esta seleccionado hoje dava um jogo cujo aspecto mudava com uma
    /// definicao de qualidade, e a diferenca nao apareceria a ninguem ate alguem a
    /// mexer.
    ///
    /// Re-executavel: nao acrescenta uma segunda feature a quem ja a tem.
    /// </summary>
    internal static class VhsWiring
    {
        private const string MaterialPath = "Assets/ThirdParty/F2F_VhsFree/vhs_Material.mat";
        private const string ShaderName = "Pungent/VHS (URP)";
        private const string FeatureName = "Pungent VHS";

        [MenuItem("Pungent/Blockout/Wire VHS", false, 55)]
        internal static void Wire()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError("[VHS] Sem material em " + MaterialPath + ".");
                return;
            }

            var log = new List<string>();

            // --- o shader ---
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[VHS] Sem o shader '" + ShaderName + "'. Compilou?");
                return;
            }

            if (material.shader != shader)
            {
                // Os valores do material do pacote sobrevivem: o porte tem os mesmos
                // nomes de propriedade de propósito, e trocar o shader mantem os que
                // baterem certo. E por isso que a afinacao do autor nao se perde.
                material.shader = shader;
                EditorUtility.SetDirty(material);
                log.Add("  shader trocado para o porte URP (bleed/fisheye/noise mantidos)");
            }
            else log.Add("  shader ja era o porte URP");

            // --- o ruido ---
            //
            // O material do pacote vem com o `_NoiseTex` a apontar para um GUID que
            // **nao existe na pasta distribuida** — ficou a apontar para o projecto de
            // quem o fez. Sem textura, o `SAMPLE_TEXTURE2D` devolve branco, o `n` fica
            // fixo em 1, e o grao transforma-se num clareamento constante de 0,0125:
            // o efeito parece estar la, esta mais claro, e nao tem grao nenhum. Isto
            // ja esta corrigido no asset; a verificacao fica porque uma reimportacao
            // do pacote volta a por o GUID errado e ninguem repara a olho.
            var noise = material.GetTexture("_NoiseTex");
            if (noise == null)
            {
                Debug.LogError("[VHS] O material esta sem `_NoiseTex`. O grao vai ficar " +
                               "morto: sem textura o ruido le-se sempre a 1 e vira um " +
                               "clareamento fixo, sem uma unica particula a mexer.");
                return;
            }
            log.Add("  ruido: " + noise.name);

            // --- os renderers ---
            int touched = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // **So os do projecto.** O `FindAssets` tambem encontra o
                // `UniversalRendererData` que vem dentro do proprio pacote do URP —
                // o modelo a partir do qual o Unity cria os renderers novos. Escrever
                // la dentro nao da erro nenhum: escreve mesmo, dentro do
                // `Library/PackageCache`, e a partir dai todo o renderer criado de
                // raiz nasce com um VHS que ninguem pediu. Ate a proxima vez que o
                // pacote for resolvido, em que desaparece sem aviso.
                if (!path.StartsWith("Assets/")) continue;

                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                log.Add("  " + System.IO.Path.GetFileNameWithoutExtension(path) + ": " +
                        Ensure(data, material));
                touched++;
            }

            if (touched == 0)
            {
                Debug.LogError("[VHS] Nao encontrei nenhum `UniversalRendererData`.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[VHS] Ligado em " + touched + " renderer(s).\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Acrescenta a feature a um renderer, ou actualiza a que ja la esta.
        ///
        /// A feature e um asset **filho** do renderer, e nao um ficheiro solto: e
        /// assim que o URP as guarda. Criar um `.asset` proprio e arrasta-lo para a
        /// lista da um segundo exemplar vivo, e a partir dai ha dois VHS a correr um
        /// por cima do outro — o dobro do grao e metade dos frames.
        /// </summary>
        private static string Ensure(UniversalRendererData data, Material material)
        {
            foreach (var existing in data.rendererFeatures)
            {
                if (!(existing is VhsRendererFeature vhs)) continue;

                Configure(vhs, material);
                return "ja tinha a feature; material reconfirmado";
            }

            var feature = ScriptableObject.CreateInstance<VhsRendererFeature>();
            feature.name = FeatureName;
            Configure(feature, material);

            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);

            // Sem isto a lista fica certa em memoria e vazia no ficheiro: o
            // `UniversalRendererData` guarda tambem uma lista de GUIDs em paralelo, e
            // quem a recarrega do disco e ela.
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            data.SetDirty();

            return "feature acrescentada";
        }

        /// <summary>
        /// O material e o ponto de injeccao, reafirmados os dois.
        ///
        /// O ponto de injeccao e reposto **mesmo em features que ja existiam**, e nao
        /// so nas novas. Mudar o valor por omissao de um `[SerializeField]` nao mexe
        /// em instancias ja gravadas, e uma feature gravada com
        /// `AfterRenderingPostProcessing` fica a correr sem se ver: o filtro e
        /// calculado e o resultado deitado fora, porque nesse ponto o alvo de cor da
        /// camara ja nao e o que vai para o ecra. Nao ha erro nenhum a dizer isso.
        /// </summary>
        private static void Configure(VhsRendererFeature feature, Material material)
        {
            var so = new SerializedObject(feature);
            so.FindProperty("material").objectReferenceValue = material;
            so.FindProperty("injection").intValue =
                (int)UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingPostProcessing;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }
    }
}
