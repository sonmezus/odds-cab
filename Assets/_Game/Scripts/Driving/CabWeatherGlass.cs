using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Actual window surfaces carrying condensation and running rain; no opaque-texture copy or refraction pass.</summary>
    public sealed class CabWeatherGlass : MonoBehaviour
    {
        static readonly int FogId = Shader.PropertyToID("_FogAmount");
        static readonly int RainId = Shader.PropertyToID("_RainAmount");
        static readonly int InteriorId = Shader.PropertyToID("_Interior");
        static readonly int WipedId = Shader.PropertyToID("_WipedGlass");
        readonly Material[] _materials = new Material[3];
        readonly Renderer[] _windows = new Renderer[10];
        WeatherPreset _weather = (WeatherPreset)(-1);
        int _count;

        public static CabWeatherGlass Create(TaxiController taxi, Transform exterior, Transform cabin)
        {
            var shader = Resources.Load<Shader>("Shaders/CabWeatherGlass");
            if (shader == null) return null;
            var root = new GameObject("TaxiWeatherGlass");
            root.transform.SetParent(taxi.transform,false);
            var glass = root.AddComponent<CabWeatherGlass>();
            for(int i=0;i<glass._materials.Length;i++)
            {
                var material=new Material(shader) { name="TaxiMoisture_"+i, hideFlags=HideFlags.DontSave };
                material.SetFloat(InteriorId,i==0?1f:0f);
                material.SetFloat(WipedId,i==2?1f:0f);
                glass._materials[i]=material;
            }
            const float cx=TaxiController.BackSeatCenterX;
            glass.Window(cabin,"RearCondensation",new Vector3(cx,1.19f,-1.46f),Quaternion.identity,new Vector2(.84f,.39f),0,TaxiController.CabLayer);
            glass.Window(cabin,"LeftCondensation",new Vector3(cx-.548f,1.18f,-.48f),Quaternion.Euler(0f,90f,0f),new Vector2(1.72f,.40f),0,TaxiController.CabLayer);
            glass.Window(cabin,"RightCondensation",new Vector3(cx+.548f,1.18f,-.48f),Quaternion.Euler(0f,90f,0f),new Vector2(1.72f,.40f),0,TaxiController.CabLayer);
            if(!glass.TryCreateCurvedGlass(exterior))
            {
                glass.Window(exterior,"WindshieldRain",new Vector3(0f,1.04f,.69f),Quaternion.Euler(26f,0f,0f),new Vector2(1.11f,.43f),2,TaxiController.ExteriorLayer);
                glass.Window(exterior,"RearWindowRain",new Vector3(0f,1.08f,-1.16f),Quaternion.Euler(-17f,0f,0f),new Vector2(.99f,.35f),1,TaxiController.ExteriorLayer);
                glass.Window(exterior,"LeftWindowRain",new Vector3(-.663f,1.04f,-.19f),Quaternion.Euler(0f,90f,0f),new Vector2(1.12f,.37f),1,TaxiController.ExteriorLayer);
                glass.Window(exterior,"RightWindowRain",new Vector3(.663f,1.04f,-.19f),Quaternion.Euler(0f,90f,0f),new Vector2(1.12f,.37f),1,TaxiController.ExteriorLayer);
                var leftDoor=FindNamed(exterior,"DoorL");
                var rightDoor=FindNamed(exterior,"DoorR");
                if(leftDoor!=null)glass._windows[5].transform.SetParent(leftDoor,true);
                if(rightDoor!=null)glass._windows[6].transform.SetParent(rightDoor,true);
            }
            glass.Apply(WeatherPreset.Sunny);
            return glass;
        }

        bool TryCreateCurvedGlass(Transform exterior)
        {
            if(!GlbModelLoader.TaxiHasOwnSeats)return false;
            var prefab=Resources.Load<GameObject>("ReferenceKit/Models/TaxiWeatherShell");
            var mesh=exterior.Find("TaxiMesh");
            if(prefab==null||mesh==null)return false;
            var shell=Instantiate(prefab,mesh,false);
            shell.name="CurvedTaxiWeatherGlass";
            var renderers=shell.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i];
                renderer.gameObject.layer=TaxiController.ExteriorLayer;
                renderer.sharedMaterial=_materials[renderer.name=="Windshield"?2:1];
                Configure(renderer);
                _windows[_count++]=renderer;
                Transform door=renderer.name=="DoorLWeather"?FindNamed(mesh,"DoorL"):renderer.name=="DoorRWeather"?FindNamed(mesh,"DoorR"):null;
                if(door!=null)renderer.transform.SetParent(door,true);
            }
            return true;
        }

        static Transform FindNamed(Transform root,string name)
        {
            if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++)
            {
                var child=FindNamed(root.GetChild(i),name);
                if(child!=null)return child;
            }
            return null;
        }

        void Window(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size,int material,int layer)
        {
            var window=GameObject.CreatePrimitive(PrimitiveType.Quad);
            window.name=name;
            window.layer=layer;
            window.transform.SetParent(parent,false);
            window.transform.SetLocalPositionAndRotation(position,rotation);
            window.transform.localScale=new Vector3(size.x,size.y,1f);
            var collider=window.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=window.GetComponent<Renderer>();
            renderer.sharedMaterial=_materials[material];Configure(renderer);
            _windows[_count++]=renderer;
        }

        static void Configure(Renderer renderer)
        {
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        void LateUpdate()
        {
            var weather=WeatherDirector.Instance;
            if(weather!=null&&weather.CurrentWeather!=_weather)Apply(weather.CurrentWeather);
        }

        public void Apply(WeatherPreset weather)
        {
            _weather=weather;
            bool fog=weather==WeatherPreset.FoggyMorning, rain=weather==WeatherPreset.Rain;
            for(int i=0;i<_materials.Length;i++)
            {
                _materials[i].SetFloat(FogId,fog?(i==0?1f:.72f):rain?(i==0?.34f:.08f):0f);
                _materials[i].SetFloat(RainId,rain?1f:0f);
            }
            for(int i=0;i<_count;i++)if(_windows[i]!=null)_windows[i].enabled=fog||rain;
        }

        void OnDestroy()
        {
            for(int i=0;i<_materials.Length;i++)if(_materials[i]!=null)Destroy(_materials[i]);
        }
    }
}
