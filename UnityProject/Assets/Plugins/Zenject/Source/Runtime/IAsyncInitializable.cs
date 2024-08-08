using Cysharp.Threading.Tasks;

namespace Zenject
{
	public interface IAsyncInitializable
	{
		UniTask AsyncInitialize();
	}
}