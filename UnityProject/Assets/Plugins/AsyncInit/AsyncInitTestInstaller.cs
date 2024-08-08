using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Plugins.AsyncInit
{
	public class AsyncInitTestInstaller : MonoInstaller
	{
		public override void InstallBindings()
		{
			Container.BindInterfacesTo<HeroSpawner>().AsSingle().NonLazy();
			Container.BindInterfacesTo<CutsceneRunnes>().AsSingle().NonLazy();
			Container.BindInterfacesTo<Ticka>().AsSingle().NonLazy();
			Container.BindInitializationAfter( typeof(CutsceneRunnes), typeof(HeroSpawner) );
		}
	}

	public class Ticka : ITickable
	{
		public void Tick()
		{
			Debug.Log( $"TICK!" );
		}
	}

	public class HeroSpawner : IAsyncInitializable
	{
		public async UniTask AsyncInitialize()
		{
			Debug.Log( $"Init async start" );
			await UniTask.Delay( 500 );
			Debug.Log( $"Init async finish" );
		}
	}

	public class CutsceneRunnes : IInitializable
	{
		public void Initialize()
		{
			Debug.Log( $"Cutscene Init" );
		}
	}
}