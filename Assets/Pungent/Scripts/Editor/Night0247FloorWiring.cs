using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Monta o Rui caido no chao da cozinha, na noite das 02:47.
    ///
    /// ---
    ///
    /// **Porque e nesta noite e nao no Dia 3.** O Dia 3 inteiro assenta na
    /// ausencia dele — ver o `DayThreeStage` — e um corpo na cozinha responde logo
    /// de manha a pergunta que o dia existe para deixar em aberto. Aqui, pelo
    /// contrario, o jogador ja esta a atravessar uma casa escura as tres da manha
    /// com a internet em baixo: esta preparado para o pior e o pior nao acontece,
    /// que e o registo do jogo.
    ///
    /// **Encaixa no passo que ja la esta.** A noite corre
    /// `RestartRouter → TalkToRui → …`. O Rui cai no instante em que o router fica
    /// pronto — com o jogador na outra ponta da casa, junto a porta da rua — e o
    /// objectivo seguinte manda-o justamente a cozinha procura-lo. Nao ha passo
    /// novo, nao ha objectivo novo, e a conversa que ja existe acontece com ele a
    /// levantar-se do chao em vez de encostado ao fogao.
    ///
    /// ---
    ///
    /// **A poca sao tres discos e nao um plano com textura.** Um quadrado com
    /// arestas rectas denuncia-se de qualquer angulo, e uma textura com alfa era
    /// mais um ficheiro para manter. Tres cilindros esmagados e sobrepostos, de
    /// tamanhos diferentes, dao uma mancha irregular que a esta luz nao se
    /// distingue de uma poca — e sao geometria, portanto apanham a luz da cozinha
    /// como o resto do chao.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class Night0247FloorWiring
    {
        private const string BrokenBottle =
            "Assets/Mess Maker Free/Prefabs/High Poly/Glass Bottles/Wine Bottle Red Broke.prefab";
        private const string SecondShard =
            "Assets/Mess Maker Free/Prefabs/High Poly/Glass Bottles/Wine Bottle Red Broke 2.prefab";
        private const string WineMaterial = "Assets/Pungent/Materials/M_WineSpill.mat";

        /// <summary>Onde o corpo fica. Meio da cozinha, a vista da porta dela.</summary>
        private static readonly Vector3 Lying = new Vector3(-5.20f, 0f, 3.40f);
        private const float LyingYaw = 90f;

        [MenuItem("Pungent/Blockout/Wire Night 02:47 Floor", false, 57)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireNightFloor")) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Apartment"))
            {
                Debug.LogError("[Chao 02:47] Abre a cena do apartamento primeiro.");
                return;
            }

            var rui = ApartmentV2WiringUtil.Find(scene, "NPC_Rui");
            if (rui == null) { Debug.LogError("[Chao 02:47] Sem NPC_Rui."); return; }

            var quest = Object.FindObjectOfType<OpeningQuestDirector>();
            if (quest == null)
                Debug.LogWarning("[Chao 02:47] Sem OpeningQuestDirector — a janela fica por ligar.");

            // ---- o grupo ----
            var group = ApartmentV2WiringUtil.Find(scene, "NIGHT_0247")
                        ?? new GameObject("NIGHT_0247");

            var host = group.transform.Find("FLOOR_RuiDown");
            if (host != null) Object.DestroyImmediate(host.gameObject);

            var holder = new GameObject("FLOOR_RuiDown");
            holder.transform.SetParent(group.transform, false);
            holder.transform.position = Lying;

            // ---- a sujidade, num grupo proprio ----
            //
            // **Separada das fontes de som de proposito.** Isto e desligado ate ele
            // cair (ver `RuiOnTheFloor.mess`), e as duas fontes tem de continuar
            // vivas: a da garrafa a partir toca no mesmo instante em que o grupo
            // acende, e um `PlayOneShot` numa fonte que acabou de ser activada no
            // mesmo frame e uma maneira barata de ficar sem o som que faz a cena.
            var mess = new GameObject("MESS");
            mess.transform.SetParent(holder.transform, false);
            mess.transform.localPosition = Vector3.zero;

            // ---- a poca ----
            var wine = EnsureWineMaterial();
            var pool = new GameObject("WinePool");
            pool.transform.SetParent(mess.transform, false);
            pool.transform.localPosition = Vector3.zero;

            // Tres discos de tamanhos e centros diferentes. Os deslocamentos sao a
            // mao de proposito: uma poca simetrica le-se como um objecto.
            // Espalhada a partir da cabeca e do ombro, que e onde a garrafa caiu —
            // e nao centrada no corpo. Uma poca simetrica por baixo de uma pessoa
            // le-se como sombra; uma que sai de um ponto e corre para o lado le-se
            // como uma coisa que foi entornada.
            // Cinco e nao tres: com tres, o disco maior deixa um arco de
            // circunferencia perfeito na borda e a poca passa a ler-se como um
            // tapete redondo. Os dois pequenos existem so para partir esse arco,
            // e por isso ficam **na berma** do grande e nao dentro dele.
            AddBlob(pool.transform, wine, new Vector3(0.38f, 0f, 0.02f), new Vector3(1.55f, 1f, 1.05f));
            AddBlob(pool.transform, wine, new Vector3(-0.16f, 0f, 0.24f), new Vector3(0.98f, 1f, 0.72f));
            AddBlob(pool.transform, wine, new Vector3(0.80f, 0f, -0.20f), new Vector3(0.72f, 1f, 0.60f));
            AddBlob(pool.transform, wine, new Vector3(1.02f, 0f, 0.34f), new Vector3(0.46f, 1f, 0.30f));
            AddBlob(pool.transform, wine, new Vector3(-0.02f, 0f, -0.42f), new Vector3(0.58f, 1f, 0.26f));

            // ---- os vidros ----
            PlacePrefab(BrokenBottle, mess.transform, new Vector3(0.88f, 0f, 0.30f), 24f);
            PlacePrefab(SecondShard, mess.transform, new Vector3(-0.50f, 0f, -0.26f), 200f);

            // ---- o som ----
            var breakGo = new GameObject("SFX_BottleBreak");
            breakGo.transform.SetParent(holder.transform, false);
            var breakSource = breakGo.AddComponent<AudioSource>();
            breakSource.playOnAwake = false;
            breakSource.spatialBlend = 1f;
            breakSource.minDistance = 2f;
            breakSource.maxDistance = 28f;
            breakSource.rolloffMode = AudioRolloffMode.Linear;

            var stirGo = new GameObject("SFX_GlassStir");
            stirGo.transform.SetParent(holder.transform, false);
            var stirSource = stirGo.AddComponent<AudioSource>();
            stirSource.playOnAwake = false;
            stirSource.spatialBlend = 1f;
            stirSource.minDistance = 1f;
            stirSource.maxDistance = 12f;
            stirSource.rolloffMode = AudioRolloffMode.Linear;

            // ---- o componente ----
            var beat = holder.AddComponent<RuiOnTheFloor>();
            var animator = rui.GetComponentInChildren<Animator>(true);

            // Tudo o que esta ligado no Rui e conduzido por outra coisa. Apanhado
            // por estado e nao por nome: uma lista de tipos escrita a mao fica
            // desactualizada na primeira vez que alguem lhe acrescentar um
            // comportamento, e o sintoma seria um cadaver a andar.
            MonoBehaviour[] suppress = rui.GetComponents<MonoBehaviour>()
                .Where(m => m != null && m.enabled && !(m is RuiOnTheFloor))
                .ToArray();

            var agent = rui.GetComponent<NavMeshAgent>();
            Behaviour[] alsoOff = agent != null ? new Behaviour[] { agent } : new Behaviour[0];

            beat.EditorConfigure(rui.transform, animator, Lying, LyingYaw,
                suppress, alsoOff, null, null, stirSource, null, null, 5f, 1.6f);

            var so = new SerializedObject(beat);
            so.FindProperty("quest").objectReferenceValue = quest;
            so.FindProperty("breakSound").objectReferenceValue = breakSource;
            so.FindProperty("mess").objectReferenceValue = mess;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Desligada ja no editor. Sem isto, a cena fica gravada com a poca a
            // vista e quem abrisse o apartamento via uma cozinha suja no prologo —
            // que e exactamente o bug que isto corrige, so que na vista de cena.
            mess.SetActive(false);

            EditorUtility.SetDirty(beat);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Chao 02:47] Montado em {Lying}. A poca e os vidros ficam DESLIGADOS " +
                      "ate ele cair — estavam a vista desde o prologo. " +
                      $"Cai no passo TalkToRui e " +
                      $"levanta-se quando o virem. Suprime {suppress.Length} componentes do Rui. " +
                      "FALTAM OS DOIS SONS: SFX_BottleBreak (a garrafa a partir, ouvida da " +
                      "cozinha) e SFX_GlassStir (o vidro a mexer quando ele se apoia).");
        }

        private static void AddBlob(Transform parent, Material wine, Vector3 offset, Vector3 scale)
        {
            var blob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blob.name = "Blob";
            blob.transform.SetParent(parent, false);
            blob.transform.localPosition = offset + new Vector3(0f, 0.004f, 0f);
            blob.transform.localScale = new Vector3(scale.x, 0.004f, scale.z);
            Object.DestroyImmediate(blob.GetComponent<Collider>());
            blob.GetComponent<MeshRenderer>().sharedMaterial = wine;
            blob.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void PlacePrefab(string path, Transform parent, Vector3 offset, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning($"[Chao 02:47] Sem {path}."); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 78f);
        }

        /// <summary>
        /// O vinho: escuro, molhado e nada vermelho-vivo.
        ///
        /// Um vermelho de sangue de filme entrega a piada antes de o jogador chegar
        /// ao pe. Isto tem de poder ser vinho a luz acesa e outra coisa qualquer no
        /// escuro, e o que faz esse trabalho e o brilho — uma poca reflecte, e e o
        /// reflexo que a faz parecer liquida em vez de pintada.
        /// </summary>
        private static Material EnsureWineMaterial()
        {
            // **Escuro, mas nao preto.** A primeira versao usava um vermelho quase
            // a zero e num chao de azulejo branco lia-se como a sombra do proprio
            // corpo — que e o oposto do que se quer, porque uma sombra nao levanta
            // pergunta nenhuma. Tem de ser inequivocamente uma mancha.
            //
            // E muito brilhante, que e o que o faz sobreviver as 02:47 com as luzes
            // apagadas: com pouca luz ambiente a cor quase desaparece, mas a
            // lanterna do telemovel arranca-lhe um reflexo que nenhuma sombra tem.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WineMaterial);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WineMaterial);
            }

            mat.SetColor("_BaseColor", new Color(0.21f, 0.017f, 0.032f, 1f));
            mat.SetFloat("_Smoothness", 0.92f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
