using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga o Dia 4 a oficina.
    ///
    /// O `CH_Day4_Garage` ja existe e espera por `arrived_garage`, `seller_met` e
    /// `parts_loaded`. Nenhum era levantado por ninguem, o que e exactamente a
    /// armadilha que este projecto ja pagou tres vezes: um passo preso a um evento
    /// sem dono bloqueia tudo o que vem depois, e nao da erro nenhum a dizer porque.
    ///
    /// Cada um passa a ter dono aqui, e todos com o mesmo componente generico em vez
    /// de uma classe por capitulo:
    ///
    /// - `arrived_garage` — chegar ao patio. Volume no chao, a entrada;
    /// - `seller_met` — falar com quem esta no escritorio;
    /// - `parts_loaded` — carregar as pecas da palete.
    ///
    /// A ordem esta garantida pelas dependencias (`requiresEvent`) e nao pela
    /// esperanca de o jogador fazer as coisas pela ordem certa: carregar as pecas
    /// antes de falar com o vendedor deixaria o capitulo a meio sem se perceber.
    /// </summary>
    internal static class GarageWiring
    {
        private const string Root = "GARAGE_EVENTS";
        private const string SunName = "GARAGE_SUN";

        /// <summary>
        /// Liga o ponto de saida do carro.
        ///
        /// ---
        ///
        /// **O Dia 4 estava intransponivel por causa de uma referencia vazia.**
        ///
        /// O capitulo comeca com o Tomas ao volante — ele conduz ate a unidade 7 —
        /// e continua a pe. Sair era uma referencia por ligar: o `exitPoint` do
        /// `CarSeat` estava a nulo, e sem ele o `Exit` larga o jogador **na posicao
        /// do mundo em que ele estava sentado**, que fica 1,65 m abaixo do banco
        /// porque a raiz do prefab esta aos pes e o banco marca os olhos. Medido:
        /// jogador em y = -0,56 com o chao a zero. Debaixo do alcatrao, com o
        /// `CharacterController` a ser religado dentro da geometria, e a consola
        /// calada.
        ///
        /// O `PLAYER_SPAWN` ja estava na cena, em (-1,20 / 0,10 / -17,00), a espera
        /// de alguem lho ligar desde que a garagem foi montada.
        /// </summary>
        private static void ReleaseThePlayerFromTheCar()
        {
            var seat = Object.FindObjectOfType<Pungent.Driving.CarSeat>(true);
            if (seat == null) { Debug.LogWarning("[Garage] Sem CarSeat na cena."); return; }

            var spawn = GameObject.Find("PLAYER_SPAWN");
            if (spawn == null)
            {
                Debug.LogWarning("[Garage] Sem `PLAYER_SPAWN`. O ponto de saida fica por " +
                                 "ligar e o `CarSeat` cai na rede dele — ao lado da porta, " +
                                 "com aviso.");
                return;
            }

            PlaceExitBesideTheDriverDoor(spawn.transform);

            var so = new SerializedObject(seat);
            var field = so.FindProperty("exitPoint");
            bool was = field.objectReferenceValue != null;
            field.objectReferenceValue = spawn.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seat);

            Debug.Log("[Garage] Ponto de saida do carro " + (was ? "reconfirmado" : "LIGADO") +
                      " em " + spawn.transform.position.ToString("F2") + ". Sem ele o jogador " +
                      "saia para 1,65 m abaixo do chao.");
        }

        /// <summary>
        /// Poe o ponto de saida ao lado da porta do condutor, fora da caixa do carro.
        ///
        /// ---
        ///
        /// **Ligar o ponto nao chegava: ele estava dentro do carro.**
        ///
        /// O `PLAYER_SPAWN` da cena estava em (-1,20 / 0,10 / -17,00), que e o
        /// **centro** do `BoxCollider` do CAR — a caixa vai de x -2,19 a -0,21 e de
        /// z -18,93 a -14,09. Sair para la punha o `CharacterController` a
        /// despenetrar para cima, e o jogador acabava **em cima do tejadilho**, com
        /// `isGrounded` a dizer que sim. Medido: y = 1,50 com o chao a zero.
        ///
        /// Trocar um numero escrito a mao por outro numero escrito a mao resolvia
        /// hoje e voltava a partir-se assim que alguem mexesse o carro. Isto sai das
        /// **bounds do colisor**: encosta-se a face do lado do condutor e afasta-se
        /// o raio do jogador mais uma folga. E o mesmo principio dos destinos das
        /// tarefas domesticas, que ja saem da malha dos moveis.
        ///
        /// Virado para o portao e nao para o carro: quem sai ja esta a olhar para
        /// onde tem de ir.
        /// </summary>
        private static void PlaceExitBesideTheDriverDoor(Transform spawn)
        {
            var car = Object.FindObjectOfType<Pungent.Driving.CarDriver>(true);
            if (car == null) return;

            var box = car.GetComponent<Collider>();
            if (box == null) box = car.GetComponentInChildren<Collider>(true);
            if (box == null) return;

            var bounds = box.bounds;

            // O lado do condutor e o -X local do carro. Metade da largura, mais o
            // raio da capsula (0,30) e uma folga que aguenta o `skinWidth`.
            const float playerRadius = 0.30f;
            const float clearance = 0.45f;

            Vector3 side = -car.transform.right;
            Vector3 point = new Vector3(bounds.center.x, 0f, bounds.center.z)
                          + side * (bounds.extents.x + playerRadius + clearance);

            // Assente no chao e nao na altura do colisor.
            point.y = bounds.min.y > 0.2f ? 0.05f : Mathf.Max(0.05f, bounds.min.y);

            spawn.position = point;
            spawn.rotation = Quaternion.LookRotation(car.transform.forward, Vector3.up);
            EditorUtility.SetDirty(spawn);

            // A verificacao que faltava da primeira vez: o ponto esta mesmo livre?
            var hits = Physics.OverlapCapsule(point + Vector3.up * 0.35f,
                                              point + Vector3.up * 1.45f, playerRadius);
            foreach (var h in hits)
            {
                if (h.isTrigger) continue;
                Debug.LogWarning("[Garage] O ponto de saida do carro toca em `" + h.name +
                                 "`. O jogador vai ser empurrado ao sair. Afastar o carro " +
                                 "da parede ou aumentar a folga.");
                break;
            }
        }

        /// <summary>
        /// O sol das dez da manha.
        ///
        /// ---
        ///
        /// **A oficina nao tinha uma unica luz direccional.** So cinco lampadas de
        /// sodio, que a §10.3 pede para a zona industrial — e que sao uma escolha de
        /// **noite**. O cartao deste capitulo diz `THURSDAY 10:02`. Sem sol, o
        /// interior fica a ler-se pelas ilhas de laranja e o patio fica praticamente
        /// preto: o relatorio de teste diz *"everything is too dark"*, e esta certo.
        ///
        /// As lampadas ficam. Uma oficina com os sodios acesos as dez da manha nao e
        /// um erro — e o que se ve em qualquer unidade sem janelas.
        ///
        /// **Vive fora do `GARAGE_BLOCKOUT` de proposito.** O `Rebuild Garage`
        /// destroi essa raiz inteira, e o sol nao pode desaparecer sempre que alguem
        /// mexer numa parede. Aqui sobrevive, e esta ferramenta corre depois daquela
        /// de qualquer maneira.
        /// </summary>
        private static void BuildMorningSun()
        {
            var old = GameObject.Find(SunName);
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject(SunName);
            Undo.RegisterCreatedObjectUndo(go, "Garage sun");

            // Baixo e do sul-poente: as dez da manha o sol entra pelo portao, que e
            // por onde o jogador chega e para onde ele olha.
            go.transform.rotation = Quaternion.Euler(38f, 205f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            // O ambiente tambem: com o skybox por omissao e sem sol, as sombras do
            // patio ficam pretas em vez de azuis, e o que se le e uma cena por
            // acender e nao uma manha.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.60f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.22f);

            Debug.Log("[Garage] Sol das 10:02 montado (direccional, fora do blockout para " +
                      "sobreviver ao 'Rebuild Garage'), e o ambiente passou a trilight.");
        }

        [MenuItem("Pungent/Blockout/Wire Garage (Day 4)", false, 13)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireGarage")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var blockout = GameObject.Find(GarageBlockoutBuilder.RootName);
            if (blockout == null)
            {
                Debug.LogError("[Garage] Correr 'Rebuild Garage' primeiro: os eventos " +
                               "assentam nos anchors do blockout.");
                return;
            }

            var director = Object.FindObjectOfType<ChapterDirector>();
            if (director == null)
                Debug.LogWarning("[Garage] Sem ChapterDirector nesta cena. Os eventos " +
                                 "ficam montados e ligam-se sozinhos quando houver um.");

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire garage events");

            ReleaseThePlayerFromTheCar();
            BuildMorningSun();

            // Chegada: volume largo a entrada do patio. Largo de proposito — perder
            // a chegada por passar ao lado seria perder o capitulo.
            Area(root.transform, "EVT_ArrivedGarage", Anchor(blockout, "GARAGE_Arrival"),
                new Vector3(9f, 3f, 5f), "arrived_garage", requires: null,
                thought: "This is the address. There is nothing else on this street.");

            // O vendedor: um ponto de interaccao no escritorio, e agora tambem
            // alguem la de pe. O evento continua a ser levantado pelo ponto — quando
            // houver conversa a serio, passa a ser ela, e o passo nao muda.
            Vector3 sellerAt = Anchor(blockout, "GARAGE_Seller");
            // "Talk to Vitor" e nao "talk to the seller": o jogador trocou mensagens
            // com ele de manha e tem o nome gravado no telemovel. Um prompt que lhe
            // chama "o vendedor" desfaz isso e volta a tornar o homem um cargo.
            Interact(root.transform, "EVT_SellerMet", sellerAt,
                "Talk to Vitor", "seller_met", requires: "arrived_garage",
                thought: "He keeps asking what time I got here.");
            PlaceSeller(root.transform, sellerAt);

            // As pecas **nao** tem ponto de interaccao.
            //
            // Tinham: um prompt "Load the parts" que levantava o evento de uma vez,
            // sem o jogador pegar em nada. Desde que a bagageira passou a contar
            // pecas carregadas (ver `GarageDressing`), esse ponto virou um atalho
            // que saltava a mecanica toda — e pior, dava **dois donos** ao mesmo
            // acontecimento, com o mais barato a ganhar sempre.
            //
            // Quem levanta `parts_loaded` e agora a `BoxDropZone` da bagageira,
            // quando as tres pecas la estiverem.

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Garage] Dia 4 ligado: arrived_garage -> seller_met -> parts_loaded.");
        }

        /// <summary>
        /// O vendedor, de pe onde o passo o espera.
        ///
        /// Cara diferente da do Rui de proposito: sao do mesmo pack e a semelhanca
        /// entre eles seria lida como pista, quando a ligacao entre os dois e para
        /// ser descoberta pelo que se diz e nao pelo que se parece.
        ///
        /// Virado para o portao — e por ali que o Tomas entra, e alguem que espera
        /// por uma entrega esta a olhar para a porta muito antes de o carro chegar.
        /// </summary>
        private static void PlaceSeller(Transform parent, Vector3 position)
        {
            var holder = new GameObject("SELLER");
            holder.transform.SetParent(parent, false);
            holder.transform.position = position;

            // O portao e a sul (§10.3): virado para -Z fica de frente para quem entra.
            holder.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            if (PsxCharacter.Place(holder.transform, PsxCharacter.Seller, "Seller_Body") == null)
                Debug.LogWarning("[Garage] Vendedor sem modelo; o ponto de conversa fica na mesma.");

            // O que ele diz, e a maneira como se aproxima do carro quando o Tomas
            // comeca a carregar. O carro so existe depois de `Dress Garage`, por
            // isso fica por resolver no `Awake` se ainda nao la estiver.
            var beats = holder.AddComponent<Pungent.Narrative.SellerBeats>();
            var driver = Object.FindObjectOfType<Pungent.Driving.CarDriver>();
            if (driver != null) beats.EditorConfigure(driver.transform);
        }

        private static Vector3 Anchor(GameObject blockout, string name)
        {
            var found = FindDeep(blockout.transform, name);
            if (found != null) return found.position;

            Debug.LogWarning($"[Garage] Anchor '{name}' nao encontrado; fica na origem.");
            return Vector3.zero;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void Area(Transform parent, string name, Vector3 position, Vector3 size,
            string eventId, string requires, string thought)
        {
            var go = Make(parent, name, position);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            Configure(go, eventId, ChapterEventRaiser.Trigger.EnterArea, string.Empty, thought, requires);
        }

        private static void Interact(Transform parent, string name, Vector3 position,
            string prompt, string eventId, string requires, string thought)
        {
            var go = Make(parent, name, position + Vector3.up * 1.1f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.1f, 1.4f, 1.1f);

            Configure(go, eventId, ChapterEventRaiser.Trigger.Interact, prompt, thought, requires);
        }

        private static GameObject Make(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        private static void Configure(GameObject go, string eventId,
            ChapterEventRaiser.Trigger trigger, string prompt, string thought, string requires)
        {
            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure(eventId, trigger, prompt, thought, requires);
            EditorUtility.SetDirty(raiser);
        }
    }
}
