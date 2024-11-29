using System;
using UnityEngine;

namespace VTS_XYPlugin
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }
                else
                {
                    GameObject go = new GameObject();
                    instance = go.AddComponent<T>();
                    go.name = $"[XYPlugin]{instance.GetType().Name}";
                    GameObject.DontDestroyOnLoad(go);
                    return instance;
                }
            }
        }

        public virtual void Init()
        {
        }
    }

    public class RegistrableSingleton<T>:MonoBehaviour where T: RegistrableSingleton<T>
    {
        private static T instance;

        private static Type _registeredType;

        public static T Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }
                else
                {
                    if (_registeredType == null)
                        throw new InvalidOperationException("the singleton has not been registered");
                    GameObject go = new GameObject();
                    instance = (T)go.AddComponent(_registeredType);
                    go.name = $"[XYPlugin]{_registeredType.Name}";
                    GameObject.DontDestroyOnLoad(go);
                    return instance;
                }
            }
        }

        public static void Register<Type>()
        {
            if (!typeof(T).IsAssignableFrom(typeof(Type)))
                throw new InvalidOperationException($"type[{typeof(Type).FullName}] is not assigned from type[{typeof(T).FullName}]");
            _registeredType = typeof(Type);
        }
    }
}