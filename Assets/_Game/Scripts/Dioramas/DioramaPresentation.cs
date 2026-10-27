using System.Collections.Generic;
using UnityEngine;

namespace OddsCab.Dioramas
{
    /// <summary>Shared PBR surfaces and stage-only, shadow-free three point lighting.</summary>
    public static class DioramaPresentation
    {
        const int DioramaLayer=26;
        static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>(12);
        public static bool TryStage(string name,Transform parent,out GameObject stage)
        {
            // glTFast 6 mirrors glTF X. This authored kit uses the opposite horizontal
            // frame, so its front must turn toward the puzzle camera on root -Z.
            // Rotate the imported stage only; gameplay/UI coordinates remain canonical.
            bool ready=ReferenceCityAssets.TryCreate("Puzzles/"+name,parent,Vector3.zero,Quaternion.Euler(0f,180f,0f),1f,out stage);
            if(ready)
            {
                if(name=="BalanceBridgeStage")AddWater(stage.transform,3.55f,1.98f,.355f);
                else if(name=="TorchBridgeStage")AddWater(stage.transform,7.15f,1.98f,.355f);
                SetLayer(stage.transform);
            }
            return ready;
        }
        public static void AddWater(Transform parent,float width,float depth,float height)
        {
            if(!Materials.TryGetValue("MiniatureWater",out var material))
            {
                var shader=Resources.Load<Shader>("BosphorusWater")??Shader.Find("OddsCab/Bosphorus Water");
                if(shader==null)return;
                material=new Material(shader) { name="MiniatureBosphorus",enableInstancing=true };
                BosphorusWaterLook.Apply(material,WeatherPreset.Sunny);
                material.SetVector("_WaveA",new Vector4(1,.35f,.008f,16));
                material.SetVector("_WaveB",new Vector4(-.4f,1,.005f,16));
                material.SetFloat("_WaveSpeed",.18f);material.SetFloat("_DetailScale",.28f);
                material.SetFloat("_NormalScale",.28f);material.SetFloat("_RippleStrength",.65f);
                material.SetFloat("_SunReflection",1.25f);material.SetFloat("_DeepDistance",18f);
                material.SetColor("_NearColor",new Color(.025f,.37f,.42f));
                material.SetColor("_DeepColor",new Color(.015f,.18f,.25f));
                var texture=Resources.Load<Texture2D>("Textures/BosphorusWater");
                if(texture!=null)material.SetTexture("_MainTex",texture);
                Materials.Add("MiniatureWater",material);
            }
            BosphorusWaterMesh.Create(parent,"MiniatureWaveSurface",new Vector3(0,height,0),new Vector2(width,depth),material,.20f);
        }
        public static Transform Find(Transform root,string name)
        {
            if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++) { var found=Find(root.GetChild(i),name);if(found!=null)return found; }
            return null;
        }
        public static Material Surface(string name,Color color,float smoothness,float metallic=0)
        {
            if(Materials.TryGetValue(name,out var material))return material;
            material=GlbModelLoader.MakeMat(color,smoothness,metallic);material.name="Puzzle_"+name;material.enableInstancing=true;
            var texture=Resources.Load<Texture2D>("ReferenceKit/Surfaces/"+name);if(texture!=null)material.SetTexture("_BaseMap",texture);
            var normal=Resources.Load<Texture2D>("ReferenceKit/Normals/"+name);
            if(normal!=null) { material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.22f);material.EnableKeyword("_NORMALMAP"); }
            Materials.Add(name,material);return material;
        }
        public static void AddLighting(Transform parent,float width=3.6f)
        {
            if(parent.Find("MiniatureLighting")!=null)return;
            var rig=new GameObject("MiniatureLighting").transform;rig.SetParent(parent,false);
            AddLight(rig,"WarmKey",new Vector3(-width*.65f,4.2f,-3.4f),new Color(1,.84f,.64f),1,width);
            AddLight(rig,"AzureFill",new Vector3(width*.7f,2.8f,-1),new Color(.56f,.74f,1),.38f,width);
            AddLight(rig,"AmberRim",new Vector3(.2f,3.5f,2.3f),new Color(1,.68f,.38f),.7f,width);SetLayer(parent);
        }
        static void AddLight(Transform parent,string name,Vector3 position,Color color,float intensity,float width)
        {
            var child=new GameObject(name);child.transform.SetParent(parent,false);child.transform.localPosition=position;
            child.transform.localRotation=Quaternion.LookRotation(new Vector3(0,1,0)-position);
            var light=child.AddComponent<Light>();light.type=LightType.Spot;light.color=color;light.intensity=intensity;
            light.range=width+9;light.spotAngle=85;light.shadows=LightShadows.None;light.cullingMask=1<<DioramaLayer;
        }
        public static void SetLayer(Transform root)
        { root.gameObject.layer=DioramaLayer;for(int i=0;i<root.childCount;i++)SetLayer(root.GetChild(i)); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { foreach(var material in Materials.Values)if(material!=null)Object.Destroy(material);Materials.Clear(); }
    }
}
