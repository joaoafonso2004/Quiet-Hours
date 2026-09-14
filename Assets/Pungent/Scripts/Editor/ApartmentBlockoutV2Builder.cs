using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Gera a arquitetura do blockout V2 do apartamento a partir de uma tabela de dados.
    /// Re-executavel: destroi e reconstroi apenas o root <see cref="RootName"/>.
    /// Nunca toca no root GRAYBOX antigo, no player, nas luzes nem no set dressing.
    ///
    /// Convencao: X cresce para leste, Z cresce para norte, chao a y = 0.
    /// As paredes sao definidas pela sua linha central; as exteriores tem a linha
    /// central deslocada para fora para que a face interior fique na cota util.
    /// </summary>
    public static class ApartmentBlockoutV2Builder
    {
        public const string RootName = "BLOCKOUT_V2";

        // --- Cotas mestras da planta (faces interiores uteis, em metros) ---
        public const float CeilingHeight = 2.8f;   // pe-direito alvo 2.7-2.9
        public const float SlabThickness = 0.2f;

        private const float West = -7.0f;
        private const float East = 5.8f;
        private const float South = -5.0f;
        private const float North = 5.0f;

        private const float CorridorSouth = -0.8f; // corredor de 1.5 m de largura
        private const float CorridorNorth = 0.7f;

        private const float XTomasRui = -3.4f;
        private const float XRuiBath = -0.1f;
        private const float XBathHall = 2.4f;
        private const float XKitchenDining = -3.0f;
        private const float XDiningLiving = 0.0f;
        private const float ZBathLaundry = -3.0f;

        private const float ExteriorThickness = 0.25f;
        private const float InteriorThickness = 0.12f;

        // Igual a altura do aro do modelo de porta (2.10). A 2.15 ficava uma fresta
        // visivel de 5 cm entre o topo do aro e o lintel.
        private const float DoorHead = 2.10f;
        private const float ArchHead = 2.2f;

        private static readonly float ExteriorHalf = ExteriorThickness * 0.5f;

        // Balcao / varanda a norte da sala.
        private const float BalconyWest = 0.0f;
        private const float BalconyEast = 5.0f;
        private const float BalconyDepth = 1.45f;
        private const float RailingHeight = 1.05f;
        private const float RailingThickness = 0.08f;

        // Materiais PBR Poly Haven (CC0). Ver Docs/ThirdPartyLicenses/TEXTURES_CC0_SOURCES.md
        private const string WallMaterial = "Assets/Pungent/Materials/M_PH_Wall_Plaster.mat";
        private const string CeilingMaterial = "Assets/Pungent/Materials/M_PH_Ceiling_Plaster.mat";
        private const string FloorMaterial = "Assets/Pungent/Materials/M_PH_Floor_Laminate.mat";
        private const string FloorTileMaterial = "Assets/Pungent/Materials/M_PH_Floor_Tile.mat";
        private const string WallTileMaterial = "Assets/Pungent/Materials/M_PH_Wall_Tile.mat";

        // Metros de mundo por repeticao da textura, por tipo de superficie.
        private const float WallTiling = 2.0f;
        private const float CeilingTiling = 2.5f;
        private const float LaminateTiling = 2.0f;
        private const float TileTiling = 1.0f;

        [MenuItem("Pungent/Blockout/Rebuild Apartment V2", false, 10)]
        public static void Rebuild()
        {
            if (BuildGuard.Blocked("BlockoutV2")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[BlockoutV2] Nenhuma cena valida aberta.");
                return;
            }

            GameObject existing = FindRoot(scene);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Rebuild Apartment Blockout V2");

            var structure = NewGroup("STRUCTURE", root.transform);
            var anchors = NewGroup("ROOM_ANCHORS", root.transform);
            var doorways = NewGroup("DOORWAY_ANCHORS", root.transform);

            BuildSlabs(structure.transform);
            BuildBalcony(structure.transform);

            foreach (var wall in BuildWallTable())
                EmitWall(wall, structure.transform, doorways.transform);

            BuildRoomAnchors(anchors.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[BlockoutV2] Blockout reconstruido. Area util aproximada: {ComputePlayableArea():0.0} m2.");
        }

        [MenuItem("Pungent/Blockout/Log Playable Area", false, 11)]
        public static void LogArea()
        {
            Debug.Log($"[BlockoutV2] Area util aproximada: {ComputePlayableArea():0.0} m2.");
        }

        public static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName)
                    return go;
            return null;
        }

        // ------------------------------------------------------------------
        // Tabela de dados da planta
        // ------------------------------------------------------------------

        /// <summary>Vao numa parede, medido ao longo do eixo longitudinal da parede.</summary>
        private struct Opening
        {
            public float From;
            public float To;
            public float Sill;
            public float Head;
            public string DoorwayName; // != null gera um anchor de porta

            public static Opening Door(float from, float to, string name)
                => new Opening { From = from, To = to, Sill = 0f, Head = DoorHead, DoorwayName = name };

            public static Opening Arch(float from, float to)
                => new Opening { From = from, To = to, Sill = 0f, Head = ArchHead, DoorwayName = null };

            public static Opening Window(float from, float to, float sill, float head)
                => new Opening { From = from, To = to, Sill = sill, Head = head, DoorwayName = null };
        }

        private struct WallDef
        {
            public string Name;
            public bool AlongX;      // true = parede corre no eixo X, false = eixo Z
            public float Line;       // coordenada fixa (z se AlongX, x caso contrario)
            public float Start;      // inicio no eixo longitudinal
            public float End;        // fim no eixo longitudinal
            public float Thickness;
            public float Height;
            public Opening[] Openings;
        }

        private static WallDef Wall(string name, bool alongX, float line, float start, float end,
                                    float thickness, params Opening[] openings)
        {
            return new WallDef
            {
                Name = name,
                AlongX = alongX,
                Line = line,
                Start = start,
                End = end,
                Thickness = thickness,
                Height = CeilingHeight,
                Openings = openings ?? new Opening[0]
            };
        }

        private static List<WallDef> BuildWallTable()
        {
            float xMinOuter = West - ExteriorHalf * 2f;
            float xMaxOuter = East + ExteriorHalf * 2f;

            return new List<WallDef>
            {
                // ---------- Envolvente exterior ----------
                Wall("Ext_South", true, South - ExteriorHalf, xMinOuter, xMaxOuter, ExteriorThickness,
                    Opening.Window(-5.8f, -4.2f, 1.0f, 2.2f),   // quarto Tomas
                    Opening.Window(-2.1f, -0.8f, 1.0f, 2.2f),   // quarto Rui (livre da cama)
                    Opening.Window(0.6f, 1.6f, 1.2f, 2.1f),     // lavandaria
                    Opening.Window(3.6f, 4.6f, 1.2f, 2.1f)),    // arrumos

                Wall("Ext_North", true, North + ExteriorHalf, xMinOuter, xMaxOuter, ExteriorThickness,
                    Opening.Window(-6.1f, -4.5f, 1.1f, 2.2f),   // cozinha, sobre o lava-loica
                    Opening.Door(0.3f, 1.5f, "Balcony"),        // porta da varanda
                    Opening.Window(2.2f, 4.8f, 0.9f, 2.3f)),    // janela ampla da sala

                Wall("Ext_West", false, West - ExteriorHalf, South, North, ExteriorThickness),

                Wall("Ext_East", false, East + ExteriorHalf, South, North, ExteriorThickness,
                    Opening.Door(-2.45f, -1.45f, "Front_3B")),  // Porta 3B

                // ---------- Corredor ----------
                Wall("Int_CorridorSouth", true, CorridorSouth, West, XBathHall, InteriorThickness,
                    Opening.Door(-5.55f, -4.65f, "Bedroom_Tomas"),
                    Opening.Door(-2.45f, -1.55f, "Bedroom_Rui"),
                    Opening.Door(0.45f, 1.35f, "Bathroom")),

                Wall("Int_CorridorNorth", true, CorridorNorth, West, XBathHall, InteriorThickness,
                    Opening.Door(-6.25f, -5.05f, "Kitchen"),    // vao largo: passagem principal do Rui
                    Opening.Arch(-2.7f, -1.1f)),                // vao para a zona de jantar

                // ---------- Zona privada (sul) ----------
                Wall("Int_TomasRui", false, XTomasRui, South, CorridorSouth, InteriorThickness),
                Wall("Int_RuiBath", false, XRuiBath, South, CorridorSouth, InteriorThickness),
                Wall("Int_BathHall", false, XBathHall, South, CorridorSouth, InteriorThickness),

                Wall("Int_BathLaundry", true, ZBathLaundry, XRuiBath, XBathHall, InteriorThickness,
                    Opening.Door(0.6f, 1.5f, "Laundry")),

                Wall("Int_HallStorage", true, ZBathLaundry, XBathHall, East, InteriorThickness,
                    Opening.Door(3.5f, 4.4f, "Storage")),

                // ---------- Zona social (norte) ----------
                Wall("Int_KitchenDining", false, XKitchenDining, CorridorNorth, North, InteriorThickness,
                    Opening.Door(0.95f, 1.95f, "Kitchen_Dining"),
                    Opening.Window(2.7f, 4.3f, 1.05f, 2.15f)),  // passa-pratos sobre a bancada

                Wall("Int_DiningLiving", false, XDiningLiving, CorridorNorth, North, InteriorThickness,
                    Opening.Arch(0.95f, 4.4f)),                 // arco largo sala <-> jantar
            };
        }

        private static void BuildRoomAnchors(Transform parent)
        {
            Anchor(parent, "ROOM_Bedroom_Tomas", -5.2f, -2.9f);
            Anchor(parent, "ROOM_Bedroom_Rui", -1.75f, -2.9f);
            Anchor(parent, "ROOM_Bathroom", 1.15f, -1.9f);
            Anchor(parent, "ROOM_Laundry", 1.15f, -4.0f);
            Anchor(parent, "ROOM_Hall", 4.1f, -1.9f);
            Anchor(parent, "ROOM_Storage", 4.1f, -4.0f);
            Anchor(parent, "ROOM_Corridor_West", -5.0f, -0.05f);
            Anchor(parent, "ROOM_Corridor_East", 1.0f, -0.05f);
            Anchor(parent, "ROOM_Kitchen", -5.0f, 2.85f);
            Anchor(parent, "ROOM_Dining", -1.5f, 2.85f);
            Anchor(parent, "ROOM_Living", 2.9f, 2.85f);
            Anchor(parent, "ROOM_Balcony", 2.5f, North + BalconyDepth * 0.5f + ExteriorThickness);
        }

        // ------------------------------------------------------------------
        // Geracao de geometria
        // ------------------------------------------------------------------

        private static void EmitWall(WallDef wall, Transform parent, Transform doorwayParent)
        {
            var group = NewGroup(wall.Name, parent);

            var sorted = new List<Opening>(wall.Openings);
            sorted.Sort((a, b) => a.From.CompareTo(b.From));

            float cursor = wall.Start;
            int piece = 0;

            foreach (var opening in sorted)
            {
                float from = Mathf.Max(opening.From, wall.Start);
                float to = Mathf.Min(opening.To, wall.End);
                if (to <= from) continue;

                if (from > cursor + 0.001f)
                    EmitBox(group.transform, $"{wall.Name}_{piece++}", wall, cursor, from, 0f, wall.Height);

                if (opening.Sill > 0.001f)
                    EmitBox(group.transform, $"{wall.Name}_sill{piece}", wall, from, to, 0f, opening.Sill);

                if (opening.Head < wall.Height - 0.001f)
                    EmitBox(group.transform, $"{wall.Name}_head{piece}", wall, from, to, opening.Head, wall.Height);

                if (!string.IsNullOrEmpty(opening.DoorwayName))
                    EmitDoorwayAnchor(doorwayParent, opening, wall);

                cursor = to;
                piece++;
            }

            if (wall.End > cursor + 0.001f)
                EmitBox(group.transform, $"{wall.Name}_{piece}", wall, cursor, wall.End, 0f, wall.Height);
        }

        private static void EmitBox(Transform parent, string name, WallDef wall,
                                    float from, float to, float yBottom, float yTop)
        {
            float length = to - from;
            float height = yTop - yBottom;
            if (length <= 0.001f || height <= 0.001f) return;

            float mid = (from + to) * 0.5f;
            Vector3 position = wall.AlongX
                ? new Vector3(mid, yBottom + height * 0.5f, wall.Line)
                : new Vector3(wall.Line, yBottom + height * 0.5f, mid);
            Vector3 scale = wall.AlongX
                ? new Vector3(length, height, wall.Thickness)
                : new Vector3(wall.Thickness, height, length);

            CreateBox(parent, name, position, scale, WallMaterial, WallTiling);
        }

        private static void EmitDoorwayAnchor(Transform parent, Opening opening, WallDef wall)
        {
            float mid = (opening.From + opening.To) * 0.5f;
            Vector3 position = wall.AlongX
                ? new Vector3(mid, 0f, wall.Line)
                : new Vector3(wall.Line, 0f, mid);

            var go = new GameObject($"DOORWAY_{opening.DoorwayName}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            // O forward do anchor aponta para a direcao de atravessamento do vao.
            go.transform.rotation = wall.AlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        }

        private static void BuildSlabs(Transform parent)
        {
            float xMin = West - ExteriorThickness;
            float xMax = East + ExteriorThickness;
            float zMin = South - ExteriorThickness;
            float zMax = North + ExteriorThickness;

            var floors = NewGroup("Floors", parent);

            // Chao por divisao: laminado nas zonas de estar, azulejo nas zonas de agua.
            Patch(floors.transform, "Floor_Kitchen", xMin, 0.7f, XKitchenDining, zMax, FloorTileMaterial, TileTiling);
            Patch(floors.transform, "Floor_DiningLiving", XKitchenDining, 0.7f, xMax, zMax, FloorMaterial, LaminateTiling);
            Patch(floors.transform, "Floor_Corridor", xMin, CorridorSouth, xMax, CorridorNorth, FloorMaterial, LaminateTiling);
            Patch(floors.transform, "Floor_Bedroom_Tomas", xMin, zMin, XTomasRui, CorridorSouth, FloorMaterial, LaminateTiling);
            Patch(floors.transform, "Floor_Bedroom_Rui", XTomasRui, zMin, XRuiBath, CorridorSouth, FloorMaterial, LaminateTiling);
            Patch(floors.transform, "Floor_Bathroom", XRuiBath, ZBathLaundry, XBathHall, CorridorSouth, FloorTileMaterial, TileTiling);
            Patch(floors.transform, "Floor_Laundry", XRuiBath, zMin, XBathHall, ZBathLaundry, FloorTileMaterial, TileTiling);
            Patch(floors.transform, "Floor_Hall", XBathHall, ZBathLaundry, xMax, CorridorSouth, FloorMaterial, LaminateTiling);
            Patch(floors.transform, "Floor_Storage", XBathHall, zMin, xMax, ZBathLaundry, FloorMaterial, LaminateTiling);

            CreateBox(parent, "Ceiling",
                new Vector3((xMin + xMax) * 0.5f, CeilingHeight + SlabThickness * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, SlabThickness, zMax - zMin), CeilingMaterial, CeilingTiling);
        }

        private static void Patch(Transform parent, string name, float x1, float z1, float x2, float z2,
                                  string materialPath, float tiling)
        {
            CreateBox(parent, name,
                new Vector3((x1 + x2) * 0.5f, -SlabThickness * 0.5f, (z1 + z2) * 0.5f),
                new Vector3(x2 - x1, SlabThickness, z2 - z1), materialPath, tiling);
        }

        private static void BuildBalcony(Transform parent)
        {
            var group = NewGroup("Balcony", parent);
            float zInner = North + ExteriorThickness;
            float zOuter = zInner + BalconyDepth;
            float width = BalconyEast - BalconyWest;
            float cx = (BalconyWest + BalconyEast) * 0.5f;

            CreateBox(group.transform, "Balcony_Slab",
                new Vector3(cx, -SlabThickness * 0.5f, (zInner + zOuter) * 0.5f),
                new Vector3(width, SlabThickness, BalconyDepth), FloorTileMaterial, TileTiling);

            CreateBox(group.transform, "Balcony_Rail_North",
                new Vector3(cx, RailingHeight * 0.5f, zOuter - RailingThickness * 0.5f),
                new Vector3(width, RailingHeight, RailingThickness), WallMaterial);

            CreateBox(group.transform, "Balcony_Rail_West",
                new Vector3(BalconyWest + RailingThickness * 0.5f, RailingHeight * 0.5f, (zInner + zOuter) * 0.5f),
                new Vector3(RailingThickness, RailingHeight, BalconyDepth), WallMaterial);

            CreateBox(group.transform, "Balcony_Rail_East",
                new Vector3(BalconyEast - RailingThickness * 0.5f, RailingHeight * 0.5f, (zInner + zOuter) * 0.5f),
                new Vector3(RailingThickness, RailingHeight, BalconyDepth), WallMaterial);
        }

        // ------------------------------------------------------------------
        // Utilitarios
        // ------------------------------------------------------------------

        private static GameObject NewGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Anchor(Transform parent, string name, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0f, z);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 size,
                                            string materialPath, float metresPerTile = WallTiling)
        {
            // Malha propria em vez de PrimitiveType.Cube: as UVs de um cubo do Unity
            // vao de 0 a 1 por face, o que esticaria a textura ao longo de paredes e
            // chaos grandes. Aqui as UVs sao proporcionais ao tamanho real.
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one;

            var mesh = BuildBoxMesh(size, metresPerTile);
            mesh.name = name + "_Mesh";
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<BoxCollider>().size = size;

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);
            // NavigationStatic esta deprecado: o NavMeshSurface do package AI Navigation
            // recolhe as fontes por colliders/render meshes, nao pela flag legada.

            return go;
        }

        /// <summary>
        /// Caixa com UVs a escala do mundo: cada face repete a textura a cada
        /// <paramref name="metresPerTile"/> metros, independentemente do tamanho da caixa.
        /// </summary>
        private static Mesh BuildBoxMesh(Vector3 size, float metresPerTile)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float inv = 1f / Mathf.Max(0.05f, metresPerTile);

            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);

            void Face(Vector3 normal, Vector3 right, Vector3 up, float halfWidth, float halfHeight, float depth)
            {
                int b = vertices.Count;
                Vector3 centre = normal * depth;
                vertices.Add(centre - right * halfWidth - up * halfHeight);
                vertices.Add(centre + right * halfWidth - up * halfHeight);
                vertices.Add(centre + right * halfWidth + up * halfHeight);
                vertices.Add(centre - right * halfWidth + up * halfHeight);

                for (int i = 0; i < 4; i++) normals.Add(normal);

                float u = halfWidth * 2f * inv;
                float v = halfHeight * 2f * inv;
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, v));
                uvs.Add(new Vector2(0f, v));

                // Enrolamento tem de concordar com a normal atribuida, senao a caixa
                // renderiza do avesso: as faces viradas para dentro desaparecem.
                // Validado com Vector3.Cross(b-a, c-a) . normal == +1.
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }

            Face(Vector3.right, Vector3.back, Vector3.up, hz, hy, hx);
            Face(Vector3.left, Vector3.forward, Vector3.up, hz, hy, hx);
            Face(Vector3.up, Vector3.right, Vector3.back, hx, hz, hy);
            Face(Vector3.down, Vector3.right, Vector3.forward, hx, hz, hy);
            Face(Vector3.forward, Vector3.right, Vector3.up, hx, hy, hz);
            Face(Vector3.back, Vector3.left, Vector3.up, hx, hy, hz);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Soma das areas uteis declaradas na planta, para validar o alvo 110-130 m2.</summary>
        private static float ComputePlayableArea()
        {
            float bedroomTomas = (XTomasRui - West) * (CorridorSouth - South);
            float bedroomRui = (XRuiBath - XTomasRui) * (CorridorSouth - South);
            float bathroom = (XBathHall - XRuiBath) * (CorridorSouth - ZBathLaundry);
            float laundry = (XBathHall - XRuiBath) * (ZBathLaundry - South);
            float hall = (East - XBathHall) * (CorridorSouth - ZBathLaundry);
            float storage = (East - XBathHall) * (ZBathLaundry - South);
            float hallStub = (East - XBathHall) * (CorridorNorth - CorridorSouth);
            float corridor = (XBathHall - West) * (CorridorNorth - CorridorSouth);
            float kitchen = (XKitchenDining - West) * (North - CorridorNorth);
            float dining = (XDiningLiving - XKitchenDining) * (North - CorridorNorth);
            float living = (East - XDiningLiving) * (North - CorridorNorth);

            return bedroomTomas + bedroomRui + bathroom + laundry + hall + storage +
                   hallStub + corridor + kitchen + dining + living;
        }
    }
}
