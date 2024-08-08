using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using ModestTree;
using ModestTree.Util;

namespace Zenject
{
	// Responsibilities:
	// - Run Initialize() on all Iinitializable's, in the order specified by InitPriority
	public class InitializableManager
	{
		public event Action<int> ItemInitialized;
		List<InitializableInfo> _initializables;

		public bool HasInitialized { get; protected set; }

		[Inject]
		public InitializableManager(
			[Inject( Optional = true, Source = InjectSources.Local )]
			List<IInitializable> initializables,
			[Inject( Optional = true, Source = InjectSources.Local )]
			List<IAsyncInitializable> initializablesAsync,
			[Inject( Optional = true, Source = InjectSources.Local )]
			List<ValuePair<Type, int>> priorities,
			List<ValuePair<Type, Type>> dependencies)
		{
			this.priorities = priorities;
			_initializables = new List<InitializableInfo>();

			for (int i = 0; i < initializables.Count; i++)
			{
				var initializable = initializables[ i ];

				// Note that we use zero for unspecified priority
				// This is nice because you can use negative or positive for before/after unspecified
				int priority = GetPriority( initializable.GetType() );

				var dep = dependencies.Where( x => initializable.GetType().DerivesFromOrEqual( x.First ) )
					.Select( x => x.Second )
					.ToArray();
				CheckPriorities( priority, dep );
				_initializables.Add( new InitializableInfo( initializable, null, priority, dep ) );
			}

			for (int i = 0; i < initializablesAsync.Count; i++)
			{
				var initializable = initializablesAsync[ i ];

				// Note that we use zero for unspecified priority
				// This is nice because you can use negative or positive for before/after unspecified
				int priority = GetPriority( initializable.GetType() );

				var dep = dependencies.Where( x => initializable.GetType().DerivesFromOrEqual( x.First ) )
					.Select( x => x.Second )
					.ToArray();
				CheckPriorities( priority, dep );
				_initializables.Add( new InitializableInfo( null, initializable, priority, dep ) );
			}
		}

		private int GetPriority(Type type)
		{
			var matches = priorities.Where( x => type.DerivesFromOrEqual( x.First ) ).Select( x => x.Second ).ToList();
			return matches.IsEmpty() ? 0 : matches.Distinct().Single();
		}

		private void CheckPriorities(int priority, Type[] dependencies)
		{
			foreach (Type dependency in dependencies)
			{
				var dependencyPriority = GetPriority( dependency );
				if (dependencyPriority > priority)
					throw new Exception(
						"Dependency priority must be less than or equal to the initializable priority" );
			}
		}

		public void Add(IInitializable initializable)
		{
			Add( initializable, null, 0, null );
		}

		public void Add(
			IInitializable initializable,
			IAsyncInitializable asyncInitializalbe,
			int priority,
			Type[] dependencies)
		{
			Assert.That( !HasInitialized );
			_initializables.Add(
				new InitializableInfo( initializable, asyncInitializalbe, priority, dependencies ) );
		}

		private HashSet<Type> initedTypes = new();
		private HashSet<Type> running = new();
		private List<ValuePair<Type, int>> priorities;

		public void Initialize()
		{
			InitAsync().Forget();
		}

		private async UniTaskVoid InitAsync()
		{
			Assert.That( !HasInitialized );
			_initializables = _initializables.OrderBy( x => x.Priority ).ToList();

#if UNITY_EDITOR
			foreach (var initializable in _initializables.Select( x => x.Initializable ).GetDuplicates())
			{
				Assert.That( false, "Found duplicate IInitializable with type '{0}'".Fmt( initializable.GetType() ) );
			}
#endif

			var waitForEndLayer = false;
			var repeatLayer = int.MinValue;
			var stopIndexInLayer = int.MaxValue;
			//этот массив отсортирован по приоритетам.
			//значит если я натыкаюсь на реди ту ран фолс - мне нужно на этом моменте зависнуть.
			for (var index = 0; index < _initializables.Count; index++)
			{
				InitializableInfo initializable = _initializables[ index ];
				try
				{
					if (waitForEndLayer && (initializable.Priority > repeatLayer) ||
					    (index == _initializables.Count - 1 && running.Count > 0))
					{
						await UniTask.DelayFrame( 1 );
						index = stopIndexInLayer - 1;
						waitForEndLayer = false;
						stopIndexInLayer = int.MaxValue;
						continue;
					}

					var type = initializable.Initializable == null
						? initializable.AsyncInitializable.GetType()
						: initializable.Initializable.GetType();

					if (initedTypes.Contains( type ) || running.Contains( type ))
					{
						continue;
					}

					if (ReadyToRun( initializable ) == false)
					{
						waitForEndLayer = true;
						if (stopIndexInLayer > index)
						{
							stopIndexInLayer = index;
						}

						repeatLayer = initializable.Priority;
						continue;
					}

#if ZEN_INTERNAL_PROFILING
                    using (ProfileTimers.CreateTimedBlock("User Code"))
#endif
#if UNITY_EDITOR
					using (ProfileBlock.Start( "{0}.Initialize()", type ))
#endif
					{
						if (initializable.Initializable != null)
						{
							initializable.Initializable.Initialize();
							initedTypes.Add( initializable.Initializable.GetType() );
						}
						else
						{
							InitAsync( initializable.AsyncInitializable ).Forget();
						}

						ItemInitialized?.Invoke( initializable.Priority );
					}

					if (index == _initializables.Count - 1 && running.Count > 0)
					{
						index = stopIndexInLayer - 1;
						waitForEndLayer = true;
					}
				}
				catch (Exception e)
				{
					throw Assert.CreateException(
						e, "Error occurred while initializing IInitializable with type '{0}'",
						initializable.Initializable.GetType() );
				}
			}

			while (running.Count > 0)
			{
				await UniTask.DelayFrame( 1 );
			}

			HasInitialized = true;
		}

		private bool ReadyToRun(InitializableInfo initializable)
		{
			if (initializable.Dependencies == null || initializable.Dependencies.Length == 0)
				return true;

			foreach (Type dependency in initializable.Dependencies)
			{
				if (initedTypes.Contains( dependency ) == false)
					return false;
			}

			return true;
		}

		private async UniTaskVoid InitAsync(IAsyncInitializable asyncInit)
		{
			Type type = asyncInit.GetType();
			try
			{
				running.Add( type );
				await asyncInit.AsyncInitialize();
				running.Remove( type );
				initedTypes.Add( type );
			}
			catch (Exception e)
			{
				throw Assert.CreateException(
					e, "Error occurred while initializing IInitializable with type '{0}'", type );
			}
		}

		class InitializableInfo
		{
			public IInitializable Initializable;
			public int Priority;
			public Type[] Dependencies;
			public IAsyncInitializable AsyncInitializable;

			public InitializableInfo(
				IInitializable initializable,
				IAsyncInitializable asyncInitializable,
				int priority,
				Type[] dependencies)
			{
				AsyncInitializable = asyncInitializable;
				Initializable = initializable;
				Priority = priority;
				Dependencies = dependencies;
			}
		}
	}
}