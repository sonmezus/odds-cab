using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Three prewarmed native particle pools: nearby rain, distant downpour and pavement impacts.</summary>
    public sealed class IstanbulRainVolume : MonoBehaviour
    {
        readonly ParticleSystem[] _systems=new ParticleSystem[3];
        readonly Material[] _materials=new Material[2];
        bool _raining;

        public static IstanbulRainVolume Create(Transform parent)
        {
            var root=new GameObject("IstanbulDownpour");root.transform.SetParent(parent,false);
            var rain=root.AddComponent<IstanbulRainVolume>();rain.Build();return rain;
        }

        void Build()
        {
            var shader=Resources.Load<Shader>("Shaders/IstanbulRain");
            if(shader==null){enabled=false;return;}
            _materials[0]=new Material(shader){name="IstanbulRainStreaks"};
            _materials[1]=new Material(shader){name="IstanbulPavementImpacts"};
            _materials[1].SetFloat("_Splash",1f);
            _systems[0]=CreatePool("NearDownpour",new Vector3(0f,9f,10f),new Vector3(27f,1f,42f),1600,1.0f,25f,.022f,false);
            _systems[1]=CreatePool("FarDownpour",new Vector3(0f,13f,54f),new Vector3(85f,1f,105f),1400,1.25f,24f,.018f,false);
            _systems[2]=CreatePool("PavementImpacts",new Vector3(0f,.09f,12f),new Vector3(5.5f,.01f,35f),240,.22f,.08f,.18f,true);
            ApplyTier(DeviceTierDetector.CurrentTier);
            DeviceTierDetector.TierChanged+=ApplyTier;
            SetRaining(false);
        }

        ParticleSystem CreatePool(string name,Vector3 position,Vector3 box,int maximum,float life,float speed,float size,bool splash)
        {
            var root=new GameObject(name);root.transform.SetParent(transform,false);root.transform.localPosition=position;
            var system=root.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.duration=2f;main.prewarm=true;
            main.maxParticles=maximum;main.startLifetime=life;main.startSpeed=speed;main.startSize=size;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new Color(.77f,.87f,.93f,splash?.48f:.46f);
            main.gravityModifier=0f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var shape=system.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale=splash?box:new Vector3(box.x,box.z,box.y);
            if(!splash)root.transform.localRotation=Quaternion.Euler(90f,0f,0f);
            var velocity=system.velocityOverLifetime;velocity.space=ParticleSystemSimulationSpace.World;
            // Unity validates all three linear axes as one mode, even a zero drift axis.
            velocity.x=new ParticleSystem.MinMaxCurve(-1.8f,-.7f);
            velocity.y=new ParticleSystem.MinMaxCurve(0f,0f);
            velocity.z=new ParticleSystem.MinMaxCurve(-.8f,.3f);
            velocity.enabled=!splash;
            var color=system.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
                new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(1f,.12f),new GradientAlphaKey(splash?0f:1f,1f)});
            color.color=gradient;
            if(splash)
            {
                var growth=system.sizeOverLifetime;growth.enabled=true;growth.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,.15f,1f,1.35f));
                main.startRotationX=90f*Mathf.Deg2Rad;main.startRotation3D=true;
            }
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_materials[splash?1:0];
            renderer.renderMode=splash?ParticleSystemRenderMode.HorizontalBillboard:ParticleSystemRenderMode.Stretch;
            renderer.velocityScale=.026f;renderer.lengthScale=1.45f;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
            return system;
        }

        void ApplyTier(DeviceTier tier)
        {
            bool low=tier==DeviceTier.Low;
            for(int i=0;i<_systems.Length;i++)
            {
                if(_systems[i]==null)continue;
                var emission=_systems[i].emission;
                emission.rateOverTime=i==0?(low?210f:740f):i==1?(low?110f:460f):(low?35f:115f);
            }
        }

        public void SetRaining(bool raining)
        {
            _raining=raining;
            for(int i=0;i<_systems.Length;i++)
            {
                if(_systems[i]==null)continue;
                if(raining)_systems[i].Play(false);else _systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void LateUpdate()
        {
            if(!_raining)return;
            var game=GameManager.Instance;if(game==null||game.Taxi==null)return;
            var position=game.Taxi.transform.position;position.x=0f;position.y=0f;transform.position=position;
        }

        void OnDestroy()
        {
            DeviceTierDetector.TierChanged-=ApplyTier;
            for(int i=0;i<_materials.Length;i++)
                if(_materials[i]!=null)
                {
                    if(Application.isPlaying)Destroy(_materials[i]);
                    else DestroyImmediate(_materials[i]);
                }
        }
    }
}
