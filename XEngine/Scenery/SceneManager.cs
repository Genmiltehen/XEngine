    using System.Diagnostics;
using XEngine.Core.Graphics;

namespace XEngine.Core.Scenery
{
                public sealed class SceneManager(AssetManager assets) : IDisposable
    {
        private bool _disposed = false;
        public GScene? CurrentScene { get; private set; }
        private readonly Queue<Action<SceneManager>> schedules = [];
        public AssetManager Assets => assets;

                                public void Schedule(Action<SceneManager> action) => schedules.Enqueue(action);

                                        public void SwitchTo(GScene newScene)
        {
            Schedule(UnloadScene);
            Schedule(LoadScene(newScene));
        }

        private static void UnloadScene(SceneManager mgr)
        {
            if (mgr.CurrentScene != null)
            {
                Debug.WriteLine("SceneUnloadStart");
                mgr.CurrentScene.Unload();
                mgr.Assets.UnloadSceneAssets();
                mgr.CurrentScene = null;
                Debug.WriteLine("SceneUnloadEnd");
            }
        }
        private static Action<SceneManager> LoadScene(GScene newScene)
        {
            return mgr =>
            {
                if (mgr.CurrentScene == null)
                {
                    Debug.WriteLine("SceneLoadStart");
                    newScene.Load();
                    mgr.CurrentScene = newScene;
                    Debug.WriteLine("SceneLoadEnd");
                }
                else Debug.WriteLine("[WARN]: Tried to load new scene when old was not unloaded");
            };
        }

                                        public void Update(float dt)
        {
            ProcessSchedules();
            CurrentScene?.Update(dt);
        }

                                private void ProcessSchedules()
        {
            foreach (var schedule in schedules) schedule.Invoke(this);
            schedules.Clear();
        }

                                public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CurrentScene?.Unload();
        }
    }
}
