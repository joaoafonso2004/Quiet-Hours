using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Driving;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// As duas pontas que faltavam: a estrada com mundo, e o epilogo com fim.
    ///
    /// ---
    ///
    /// **A estrada.** O `NightRoadBuilder` monta alcatrao, arvores, relva, postes,
    /// nevoeiro, radio e quem vem atras. Falta-lhe uma coisa so, e nao e arte:
    /// **mais ninguem existe naquela estrada.** Um jogador que conduza tres minutos
    /// sem cruzar um unico carro percebe, sem o saber dizer, que aquilo nao e uma
    /// estrada — e um corredor construido para ele. A partir dai nao ha nevoeiro que
    /// chegue.
    ///
    /// E resolve-se de graca, porque a noite um carro em sentido contrario sao dois
    /// farois a crescer e um segundo de barulho. Ver <see cref="OncomingTraffic"/>.
    ///
    /// O que interessa mesmo e a segunda metade: **eles param.** A partir de
    /// `road_followed` nao passa mais nenhum, e ninguem comenta. A ausencia so
    /// funciona porque houve presenca — e por isso e que isto tinha de existir antes
    /// de poder ser tirado.
    ///
    /// ---
    ///
    /// **O epilogo.** Os quatro finais existem e sao decididos bem. O que eles fazem e
    /// dizer tres frases com o jogador de pe num corredor, com o rato a funcionar,
    /// livre de ir a cozinha enquanto o jogo lhe conta como e que a historia acabou.
    ///
    /// Aqui fecha-se o ecra e da-se um som a cada final. Ver
    /// <see cref="EpilogueStage"/>: e o som, e nao as frases, que fica.
    ///
    /// ---
    ///
    /// Duas ferramentas porque sao duas cenas. Ambas re-executaveis.
    /// </summary>
    internal static class NightAndEpilogueWiring
    {
        private const string AudioFolder = "Assets/ThirdParty/Audio/";
        private const string TrafficRoot = "ONCOMING_TRAFFIC";

        // ==================================================================
        // ESTRADA
        // ==================================================================

        [MenuItem("Pungent/Blockout/Wire Oncoming Traffic (night road)", false, 18)]
        internal static void WireTraffic()
        {
            if (BuildGuard.Blocked("WireOncomingTraffic")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var roadDirector = Object.FindObjectOfType<NightRoadDirector>(true);
            if (roadDirector == null)
            {
                Debug.LogError("[Transito] Sem `NightRoadDirector` nesta cena. Abrir a " +
                               "`Night_Road` e correr 'Build Night Road' primeiro.");
                return;
            }

            // A estrada e o carro vem de quem ja os tem, e nao de um
            // `FindObjectOfType`: ha **dois** `CarDriver` nesta cena — o do Tomas e o
            // de quem vem atras — e apanhar o errado punha o transito a nascer a
            // trezentos metros do perseguidor, atras do jogador, invisivel a jogada
            // inteira.
            var so = new SerializedObject(roadDirector);
            var path = so.FindProperty("path").objectReferenceValue as RoadPath;
            var car = so.FindProperty("car").objectReferenceValue as CarDriver;

            if (path == null || car == null)
            {
                Debug.LogError("[Transito] O `NightRoadDirector` esta sem `path` ou sem " +
                               "`car`. Correr 'Build Night Road' outra vez.");
                return;
            }

            var old = GameObject.Find(TrafficRoot);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(TrafficRoot);
            Undo.RegisterCreatedObjectUndo(root, "Wire oncoming traffic");

            var traffic = root.AddComponent<OncomingTraffic>();

            var loop = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "car_pass.mp3");

            // **Sem modelos, de propósito.**
            //
            // O projecto tem dois carros: o SEDAN, que e o do Tomas, e o
            // HATCHBACK_1988, que e o de quem vem atras. Usar qualquer um deles para o
            // transito e pior do que uma caixa preta — cruzar-se com o proprio carro,
            // ou com o carro do perseguidor a vir de frente, diz ao jogador uma coisa
            // que nao e verdade no momento exacto em que ele esta a tentar perceber
            // quem e que anda ali.
            //
            // O que se ve de um carro que vem de frente, a noite, com nevoeiro, e o
            // farol. A caixa vive atras dele. Quando houver dois ou tres modelos que
            // nao sejam nenhum dos dois, entram aqui e nao muda mais nada.
            traffic.EditorConfigure(path, car, new GameObject[0], loop,
                stopsOn: "road_followed", offset: -3.5f);
            EditorUtility.SetDirty(traffic);

            EditorSceneManager.MarkSceneDirty(scene);

            var log = new List<string>
            {
                "transito em sentido contrario ligado ao percurso de " + path.Length.ToString("F0") + " m",
                "para de vez em `road_followed` — depois disso a estrada fica vazia para sempre",
                "faixa a -3,5 m do eixo; se os carros passarem por cima do alcatrao errado, " +
                "e este numero que se mexe (`laneOffset`)"
            };
            log.Add(loop != null
                ? "som: `car_pass.mp3` ligado (o efeito de passagem e feito pelo Doppler do Unity)"
                : "POR PREENCHER: falta `" + AudioFolder + "car_pass.mp3` — os carros passam mudos");
            log.Add("carros sem modelo (caixas escuras atras dos farois) — ver a nota no codigo");

            WireCrash(root.transform, roadDirector, path, car, log);

            Debug.Log("[Transito] Ligado.\n  " + string.Join("\n  ", log));
        }

        /// <summary>
        /// O despiste: corte a preto, e acordar na berma com quem vem atras mais perto.
        ///
        /// Vive no mesmo objecto do transito porque e a consequencia dele — e o unico
        /// sitio de onde pode vir uma batida de frente nesta estrada. A fonte de som do
        /// impacto fica no carro do jogador e nao aqui: e ali que o barulho acontece, e
        /// uma fonte plana numa batida faz o som vir de lado nenhum.
        /// </summary>
        private static void WireCrash(Transform parent, NightRoadDirector roadDirector,
            RoadPath path, CarDriver car, List<string> log)
        {
            var go = new GameObject("ROAD_CRASH");
            go.transform.SetParent(parent, false);

            var source = car.GetComponentInChildren<AudioSource>(true);
            if (source == null)
            {
                var holder = new GameObject("Crash_Audio");
                holder.transform.SetParent(car.transform, false);
                source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.4f;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "car_crash.mp3");

            var crash = go.AddComponent<RoadCrash>();
            crash.EditorConfigure(car, path, roadDirector,
                Object.FindObjectOfType<Pungent.Narrative.ScreenFade>(true), source, clip);
            EditorUtility.SetDirty(crash);

            log.Add("despiste: corte a preto (0,28 s), 4,5 s de nada, e acorda 30 m atras " +
                    "na berma com o motor morto");
            log.Add("quem vem atras passa de 48 m para 20 m — o preco e tempo, e nao ha ecra de derrota");
            log.Add(clip != null
                ? "som do impacto: `car_crash.mp3` ligado"
                : "POR PREENCHER: falta `" + AudioFolder + "car_crash.mp3` — a batida e muda");
        }

        // ==================================================================
        // EPILOGO
        // ==================================================================

        [MenuItem("Pungent/Blockout/Wire Epilogue (four endings)", false, 19)]
        internal static void WireEpilogue()
        {
            if (BuildGuard.Blocked("WireEpilogue")) return;

            var scene = EditorSceneManager.GetActiveScene();

            // No `GAME_SYSTEMS` e nao no jogador: o epilogo comeca depois de o climax
            // ter recarregado a cena, e o jogador dessa altura e outro objecto. Um
            // componente no `PlayerRoot` era destruido a meio da unica coisa que
            // tinha para fazer.
            var systems = Object.FindObjectOfType<GameSystemsRoot>(true);
            GameObject host = systems != null ? systems.gameObject : GameObject.Find("GAME_SYSTEMS");
            if (host == null)
            {
                Debug.LogError("[Epilogo] Sem `GAME_SYSTEMS` nesta cena.");
                return;
            }

            var stage = host.GetComponent<EpilogueStage>();
            if (stage == null) stage = Undo.AddComponent<EpilogueStage>(host);

            var log = new List<string>();

            // Os quatro sons. O tempo conta a partir do momento em que o final e
            // decidido: 4,6 s de cartao mais tres frases de 6,5 dao 24,1 s ate a
            // ultima acabar. 25,5 poe o som um segundo e meio depois dela — dentro da
            // folga que o `GameEnding` deixa antes de fechar o jogo.
            const float AfterLastLine = 25.5f;

            var endings = new[]
            {
                // Alguem leu, e respondeu. E o unico som bom do jogo inteiro.
                Ending("ending_proof", "phone_notification.mp3", AfterLastLine, 0.75f, log),

                // Nada. Ninguem perguntou nada, e ninguem vai perguntar. O silencio
                // aqui nao e conteudo em falta — e a resposta, e e a unica maneira de
                // a dizer sem a escrever.
                new EpilogueStage.Ending
                {
                    EventId = "ending_no_proof", Sound = null,
                    AfterSeconds = AfterLastLine, Volume = 0f
                },

                // Uma porta de carro a fechar, perto. Ele sabe onde tu moras.
                Ending("ending_trust", "car_door_close.mp3", AfterLastLine, 0.7f, log),

                // Uma fechadura a abrir, do lado de fora. Nao ha nada a explicar.
                Ending("ending_taken", "door_unlock.mp3", AfterLastLine, 0.8f, log),

                // O quinto. Uma porta de carro a **abrir** — a mesma familia de som do
                // final de falsa confianca, e o contrario dele: ali fecha-se atras de
                // alguem que tu nao sabes quem e, aqui abre-se a espera de ti.
                //
                // Cai mais cedo do que os outros: este final tem imagem (ver o
                // `EpilogueTableau`) e o som tem de acontecer dentro dela, e nao depois
                // de o ecra ja ter fechado.
                Ending("ending_father", "car_door_open.mp3", AfterLastLine - 4f, 0.7f, log)
            };

            stage.EditorConfigure("epilogue", Object.FindObjectOfType<ScreenFade>(true), endings);
            EditorUtility.SetDirty(stage);

            EditorSceneManager.MarkSceneDirty(scene);

            log.Insert(0, "ecra fecha a preto em `epilogue` e nao reabre; controlo suspenso");
            log.Insert(1, "`ending_no_proof` fica em silencio de propósito — ver a nota no codigo");
            Debug.Log("[Epilogo] Ligado em " + host.name + ".\n  " + string.Join("\n  ", log));
        }

        private static EpilogueStage.Ending Ending(string id, string fileName,
            float after, float volume, List<string> log)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + fileName);
            log.Add(clip != null
                ? id + ": `" + fileName + "` ligado, a " + after.ToString("F1") + " s"
                : "POR PREENCHER: falta `" + AudioFolder + fileName + "` — " + id +
                  " acaba em silencio, o que ja e o significado de outro final");

            return new EpilogueStage.Ending
            {
                EventId = id,
                Sound = clip,
                AfterSeconds = after,
                Volume = volume
            };
        }
    }
}
