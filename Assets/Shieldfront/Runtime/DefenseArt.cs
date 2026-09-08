using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shieldfront
{
    public sealed class SoldierView
    {
        public GameObject root;
        public Transform body, weapon, leftLeg, rightLeg, healthFill;
        public Vector3 lastPosition;
        public float gait, deathTime = -1;
    }

    // Original procedural models. No downloaded assets or third-party art dependencies.
    public class DefenseArt : MonoBehaviour
    {
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        public static readonly Color Blue = new Color(.13f, .38f, .59f);
        public static readonly Color Gold = new Color(.91f, .70f, .32f);
        public static readonly Color Iron = new Color(.46f, .55f, .58f);
        public static readonly Color Wood = new Color(.27f, .18f, .12f);
        public static Color Accent(TroopKind kind) => kind switch
        {
            TroopKind.Shield => Blue, TroopKind.Spear => new Color(.18f, .57f, .52f),
            TroopKind.Archer => new Color(.38f, .57f, .26f), TroopKind.Cavalry => Gold,
            TroopKind.Tower => new Color(.65f, .43f, .29f), _ => new Color(.67f, .22f, .17f)
        };
        public Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var found)) return found;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .08f);
            materials.Add(color, mat); return mat;
        }
        public Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 p, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(shape); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = p; obj.transform.localScale = size;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return obj.transform;
        }
        public Transform Box(Transform parent, string name, Vector3 p, Vector3 size, Color c) => Part(parent, name, PrimitiveType.Cube, p, size, c);
        public Transform Cone(Transform parent, Vector3 p, float radius, float height, Color color, int sides = 7)
        {
            GameObject go = new GameObject("Faceted crown"); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                int k = v.Count;
                v.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                v.Add(Vector3.up * height); v.Add(new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius));
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 2);
            }
            Mesh mesh = new Mesh { name = "Original low-poly cone" }; mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals();
            meshes.Add(mesh); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go.transform;
        }
        public Transform Line(Transform parent, string name, Vector3 a, Vector3 b, float width, Color color)
        {
            Transform line = Box(parent, name, (a + b) * .5f, new Vector3(width, (b - a).magnitude, width), color);
            line.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return line;
        }
        public GameObject Ring(Transform parent, Vector3 p, float radius, Color color)
        {
            var go = new GameObject("Formation boundary"); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true;
            line.positionCount = 48; line.widthMultiplier = .055f; line.sharedMaterial = Mat(color);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, .045f, Mathf.Sin(a) * radius)); }
            return go;
        }
        public void BuildWorld()
        {
            var world = new GameObject("THE AMBER PASS · Environment").transform; world.SetParent(transform, false);
            Box(world, "Diorama bedrock", new Vector3(1, -.75f, 0), new Vector3(44, 1.6f, 29), new Color(.26f, .29f, .22f));
            Box(world, "Meadow", new Vector3(1, .04f, 0), new Vector3(43.8f, .08f, 28.8f), new Color(.40f, .49f, .29f));
            Box(world, "Old road", new Vector3(2, .092f, 0), new Vector3(38, .02f, 3.7f), new Color(.62f, .56f, .39f));
            foreach (float z in new[] { -6f, 6f })
                Box(world, "Flank trail", new Vector3(2, .091f, z), new Vector3(38, .02f, 1.7f), new Color(.50f, .49f, .32f));
            var random = new System.Random(703);
            for (int i = 0; i < 85; i++)
            {
                float x = (float)random.NextDouble() * 40 - 18;
                float z = (i % 2 == 0 ? -1 : 1) * (10.9f + (float)random.NextDouble() * 2.3f);
                float scale = .65f + (float)random.NextDouble() * .8f;
                if (i < 60)
                {
                    Part(world, "Pine trunk", PrimitiveType.Cylinder, new Vector3(x, .7f * scale, z), new Vector3(.23f, .7f * scale, .23f), Wood);
                    Color leaf = i % 3 == 0 ? new Color(.21f, .36f, .29f) : new Color(.28f, .41f, .29f);
                    Cone(world, new Vector3(x, .6f * scale, z), 1.1f * scale, 2.4f * scale, leaf);
                    Cone(world, new Vector3(x, 1.6f * scale, z), .8f * scale, 1.8f * scale, leaf);
                }
                else
                {
                    Transform rock = Part(world, "Weathered stone", PrimitiveType.Sphere, new Vector3(x, .3f, z), new Vector3(1.6f, .9f, 1.1f), new Color(.46f, .49f, .42f));
                    rock.localRotation = Quaternion.Euler(i * 13, i * 37, i * 9);
                }
            }
            for (int i = 0; i < 70; i++)
            {
                float x = (float)random.NextDouble() * 34 - 12, z = (float)random.NextDouble() * 20 - 10;
                if (Mathf.Abs(z) < 2) continue;
                Box(world, "Meadow tuft", new Vector3(x, .14f, z), new Vector3(.15f, .22f, .12f), new Color(.57f, .59f, .33f));
            }
            BuildFortress(world);
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * 6;
                Banner(world, new Vector3(19, 0, z + 1.8f), Accent(TroopKind.Goblin), 2.4f);
                for (int j = 0; j < 3; j++) Line(world, "Attack direction", new Vector3(17 + j * .35f, .13f, z - .35f), new Vector3(16.5f + j * .35f, .13f, z), .06f, new Color(.76f, .36f, .23f));
            }
        }
        void BuildFortress(Transform world)
        {
            var stone = new Color(.63f, .63f, .52f);
            for (int i = -4; i <= 4; i++)
            {
                float z = i * 2.6f;
                if (i == 0) continue;
                Box(world, "Curtain wall", new Vector3(-15.7f, 1.5f, z), new Vector3(1.05f, 3, 2.55f), stone);
                for (int c = 0; c < 2; c++) Box(world, "Battlement", new Vector3(-15.7f, 3.25f, z - .8f + c * 1.6f), new Vector3(1.12f, .6f, .65f), stone);
            }
            foreach (float z in new[] { -2.5f, 2.5f, -11f, 11f })
            {
                Part(world, "Fortress turret", PrimitiveType.Cylinder, new Vector3(-15.7f, 2.3f, z), new Vector3(2.5f, 2.3f, 2.5f), stone);
                Cone(world, new Vector3(-15.7f, 4.6f, z), 1.65f, 1.5f, Blue);
                Banner(world, new Vector3(-15.7f, 5.8f, z), Blue, 1.7f);
            }
            Box(world, "Gatehouse arch", new Vector3(-15.7f, 3.4f, 0), new Vector3(1.5f, .9f, 3.7f), stone);
            Box(world, "Oak gate", new Vector3(-15.7f, 1.4f, 0), new Vector3(.65f, 2.8f, 2.8f), Wood);
            for (int i = -1; i <= 1; i++) Box(world, "Gate iron brace", new Vector3(-15.32f, 1.4f + i * .8f, 0), new Vector3(.06f, .1f, 2.7f), Iron);
            Box(world, "Keep", new Vector3(-18.4f, 2, 0), new Vector3(3, 4, 5), stone);
            Cone(world, new Vector3(-18.4f, 4, 0), 3.2f, 2, Blue, 4);
        }
        public void Banner(Transform parent, Vector3 p, Color color, float height)
        {
            Part(parent, "Standard pole", PrimitiveType.Cylinder, p + Vector3.up * height * .5f, new Vector3(.055f, height * .5f, .055f), Wood);
            Box(parent, "Army standard", p + new Vector3(.42f, height * .8f, 0), new Vector3(.85f, height * .35f, .045f), color);
            Box(parent, "Standard sigil", p + new Vector3(.42f, height * .8f, -.03f), new Vector3(.15f, height * .21f, .04f), Gold);
        }
        public SoldierView Soldier(TroopKind kind, Transform parent)
        {
            var view = new SoldierView();
            view.root = new GameObject(kind.ToString()); view.root.transform.SetParent(parent, false);
            Transform root = view.root.transform;
            if (kind == TroopKind.Tower)
            {
                foreach (float x in new[] { -.65f, .65f }) foreach (float z in new[] { -.65f, .65f })
                    Box(root, "Tower post", new Vector3(x, 1.5f, z), new Vector3(.22f, 3, .22f), Wood);
                Box(root, "Platform", new Vector3(0, 2.5f, 0), new Vector3(1.8f, .3f, 1.8f), Wood);
                for (int j = 0; j < 4; j++)
                {
                    float a = j * Mathf.PI * .5f;
                    var rail = Box(root, "Parapet", new Vector3(Mathf.Cos(a) * .86f, 2.9f, Mathf.Sin(a) * .86f), new Vector3(.16f, .7f, 1.9f), Wood);
                    rail.localRotation = Quaternion.Euler(0, -j * 90, 0);
                }
                Cone(root, new Vector3(0, 3.5f, 0), 1.4f, 1.1f, Blue, 4);
                view.body = root; view.weapon = Box(root, "Ballista", new Vector3(0, 3.05f, .8f), new Vector3(1.1f, .15f, .4f), Iron);
                Health(view, 5f); return view;
            }
            bool enemy = (int)kind >= 5, mounted = kind == TroopKind.Cavalry || kind == TroopKind.Raider;
            Color cloth = Accent(kind), skin = enemy ? new Color(.41f, .56f, .29f) : new Color(.87f, .67f, .45f);
            float lift = mounted ? .85f : 0;
            if (mounted)
            {
                Color horse = enemy ? new Color(.33f, .35f, .30f) : new Color(.37f, .25f, .16f);
                Part(root, "Mount body", PrimitiveType.Capsule, new Vector3(0, .95f, -.1f), new Vector3(.7f, .8f, 1.3f), horse).localRotation = Quaternion.Euler(90, 0, 0);
                Box(root, "Mount neck", new Vector3(0, 1.4f, .52f), new Vector3(.4f, .8f, .45f), horse).localRotation = Quaternion.Euler(-22, 0, 0);
                Box(root, "Mount head", new Vector3(0, 1.72f, .79f), new Vector3(.4f, .4f, .65f), horse);
                for (int i = 0; i < 4; i++) Box(root, "Mount leg", new Vector3((i % 2 == 0 ? -1 : 1) * .25f, .4f, i < 2 ? -.5f : .4f), new Vector3(.16f, .8f, .16f), horse);
                Line(root, "Tail", new Vector3(0, 1.2f, -.7f), new Vector3(0, .6f, -1), .13f, Wood);
            }
            view.body = new GameObject("Animated body").transform; view.body.SetParent(root, false); view.body.localPosition = Vector3.up * lift;
            Transform body = view.body;
            Part(body, "Tunic", PrimitiveType.Capsule, new Vector3(0, .94f, 0), new Vector3(.63f, .44f, .45f), cloth);
            Box(body, "Belt", new Vector3(0, .75f, 0), new Vector3(.66f, .12f, .48f), Wood);
            Part(body, "Face", PrimitiveType.Sphere, new Vector3(0, 1.54f, 0), new Vector3(.49f, .52f, .45f), skin);
            Part(body, "Helmet", PrimitiveType.Sphere, new Vector3(0, 1.75f, -.035f), new Vector3(.56f, .29f, .51f), enemy ? Wood : Iron);
            if (kind == TroopKind.Cavalry || kind == TroopKind.Shield) Box(body, "Helmet crest", new Vector3(0, 1.96f, -.05f), new Vector3(.1f, .22f, .5f), cloth);
            foreach (float x in new[] { -.1f, .1f }) Box(body, "Eye", new Vector3(x, 1.58f, .212f), new Vector3(.065f, .04f, .04f), new Color(.12f, .13f, .12f));
            view.leftLeg = Box(body, "Left boot", new Vector3(-.18f, .32f, .03f), new Vector3(.22f, .59f, .3f), Wood);
            view.rightLeg = Box(body, "Right boot", new Vector3(.18f, .32f, .03f), new Vector3(.22f, .59f, .3f), Wood);
            Box(body, "Left arm", new Vector3(-.4f, 1.04f, .08f), new Vector3(.21f, .56f, .24f), cloth);
            var arm = new GameObject("Weapon arm").transform; arm.SetParent(body, false); arm.localPosition = new Vector3(.4f, 1.1f, .1f); view.weapon = arm;
            Box(arm, "Right arm", Vector3.zero, new Vector3(.21f, .55f, .24f), cloth);
            if (kind == TroopKind.Shield || kind == TroopKind.Goblin || kind == TroopKind.Cavalry)
            {
                Box(body, "Shield rim", new Vector3(-.42f, 1.04f, .33f), new Vector3(.62f, .84f, .17f), Gold);
                Box(body, "Shield face", new Vector3(-.42f, 1.04f, .43f), new Vector3(.51f, .72f, .045f), cloth);
                Box(body, "Shield stripe", new Vector3(-.42f, 1.04f, .465f), new Vector3(.10f, .61f, .025f), Gold);
            }
            if (kind == TroopKind.Spear || kind == TroopKind.Cavalry)
            {
                Line(arm, "Spear shaft", new Vector3(0, -.45f, -.1f), new Vector3(0, 1.3f, .8f), .07f, Wood);
                Cone(arm, new Vector3(0, 1.3f, .8f), .12f, .45f, Iron, 4).localRotation = Quaternion.Euler(28, 0, 0);
            }
            else if (kind == TroopKind.Archer)
            {
                Line(arm, "Bow upper", new Vector3(0, 0, .4f), new Vector3(0, .55f, .15f), .07f, Gold);
                Line(arm, "Bow lower", new Vector3(0, 0, .4f), new Vector3(0, -.55f, .15f), .07f, Gold);
                Line(arm, "Bow string", new Vector3(0, -.55f, .15f), new Vector3(0, .55f, .15f), .018f, Iron);
                Box(body, "Quiver", new Vector3(.15f, 1.15f, -.37f), new Vector3(.22f, .7f, .2f), Wood);
            }
            else
            {
                Box(arm, "Hilt", new Vector3(0, -.05f, .2f), new Vector3(.32f, .09f, .12f), Gold);
                Box(arm, "Blade", new Vector3(0, .42f, .2f), new Vector3(.13f, .85f, .07f), Iron);
            }
            if (kind == TroopKind.Brute) root.localScale = Vector3.one * 1.8f;
            Health(view, 2.35f + lift); return view;
        }
        void Health(SoldierView view, float y)
        {
            Transform bg = Box(view.root.transform, "Health", new Vector3(0, y, 0), new Vector3(.9f, .075f, .075f), new Color(.13f, .19f, .17f));
            view.healthFill = Box(bg, "Fill", new Vector3(0, 0, -.2f), new Vector3(.94f, .65f, 1.2f), new Color(.53f, .83f, .46f));
        }
        public void Pose(SoldierView v, Fighter f, float dt, Camera camera)
        {
            if (!f.Alive)
            {
                if (v.deathTime < 0) v.deathTime = 0;
                v.deathTime += dt; v.root.transform.rotation *= Quaternion.Euler(0, 0, dt * 200);
                v.root.transform.position += Vector3.down * dt * 1.5f;
                if (v.deathTime > .65f) v.root.SetActive(false);
                return;
            }
            Transform t = v.root.transform; float distance = (f.position - v.lastPosition).magnitude;
            v.gait += distance * 6;
            t.position = f.position;
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(f.facing), Mathf.Min(1, dt * 14));
            if (v.leftLeg != null)
            {
                float swing = distance > .001f ? Mathf.Sin(v.gait) * 25 : 0;
                v.leftLeg.localRotation = Quaternion.Euler(swing, 0, 0); v.rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
                v.weapon.localRotation = Quaternion.Euler(-f.attackPulse * 85, 0, f.attackPulse * -12);
            }
            if (v.healthFill != null)
            {
                v.healthFill.parent.gameObject.SetActive(f.hp < f.maxHp);
                v.healthFill.parent.rotation = camera.transform.rotation;
                v.healthFill.localScale = new Vector3(.94f * f.hp / f.maxHp, .65f, 1.2f);
            }
            v.lastPosition = f.position;
        }
        void OnDestroy()
        {
            foreach (var mat in materials.Values) if (mat != null) Destroy(mat);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
