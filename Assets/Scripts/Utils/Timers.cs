using System.Collections.Generic;  // 引入泛型集合类型的命名空间
using System.Collections;  // 引入非泛型集合类型的命名空间
using UnityEngine;  // 引入Unity引擎命名空间

namespace FairyGUI  // FairyGUI命名空间
{
    // 定义一个委托类型，表示定时器的回调方法
    public delegate void TimerCallback(object param);

    /// <summary>
    /// Timers类用于管理和操作定时器
    /// </summary>
    public class Timers
    {
        public static int repeat;  // 静态字段，记录当前定时器的剩余重复次数
        public static float time;  // 静态字段，记录时间流逝

        public static bool catchCallbackExceptions = false;  // 是否捕获回调中的异常，默认为不捕获

        // 定义字典，存储已添加的定时器项
        Dictionary<TimerCallback, Anymous_T> _items;
        // 定义字典，存储待添加的定时器项
        Dictionary<TimerCallback, Anymous_T> _toAdd;
        // 定义列表，存储待移除的定时器项
        List<Anymous_T> _toRemove;
        // 定义列表，作为定时器项的对象池
        List<Anymous_T> _pool;

        // 引擎，负责定时器的更新
        TimersEngine _engine;
        // 用于持有定时器相关的GameObject对象
        GameObject gameObject;

        // 静态实例，确保全局只存在一个Timers实例
        private static Timers _inst;
        public static Timers inst
        {
            get
            {
                if (_inst == null)
                    _inst = new Timers();  // 如果实例为空，则创建一个新的Timers实例
                return _inst;
            }
        }

#if UNITY_2019_3_OR_NEWER
        // Unity 2019.3及以上版本的生命周期初始化方法
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitializeOnLoad()
        {
            _inst = null;  // 确保在加载时Timers实例为null
        }
#endif

        // 构造函数，初始化Timers实例
        public Timers()
        {
            _inst = this;  // 将当前实例赋给静态实例
            gameObject = new GameObject("[FairyGUI.Timers]");  // 创建一个新的GameObject
            gameObject.hideFlags = HideFlags.HideInHierarchy;  // 隐藏该GameObject
            gameObject.SetActive(true);  // 激活该GameObject
            Object.DontDestroyOnLoad(gameObject);  // 确保游戏场景切换时该GameObject不会销毁

            _engine = gameObject.AddComponent<TimersEngine>();  // 为GameObject添加TimersEngine组件

            _items = new Dictionary<TimerCallback, Anymous_T>();  // 初始化已添加定时器项的字典
            _toAdd = new Dictionary<TimerCallback, Anymous_T>();  // 初始化待添加定时器项的字典
            _toRemove = new List<Anymous_T>();  // 初始化待移除定时器项的列表
            _pool = new List<Anymous_T>(100);  // 初始化定时器项的对象池，最多100个
        }

        // 添加定时器，默认回调参数为空
        public void Add(float interval, int repeat, TimerCallback callback)
        {
            Add(interval, repeat, callback, null);  // 调用重载方法，传入null作为回调参数
        }

        /**
         * @interval 定时器间隔时间（秒）
         * @repeat 重复次数，0表示无限循环，否则表示执行的次数
         **/
        public void Add(float interval, int repeat, TimerCallback callback, object callbackParam)
        {
            // 如果回调函数为空，输出警告并返回
            if (callback == null)
            {
                Debug.LogWarning("timer callback is null, " + interval + "," + repeat);
                return;
            }

            Anymous_T t;  // 临时定时器项
            // 检查定时器是否已经存在，如果存在，则更新定时器的设置
            if (_items.TryGetValue(callback, out t))
            {
                t.set(interval, repeat, callback, callbackParam);  // 设置定时器项的属性
                t.elapsed = 0;  // 重置已过去的时间
                t.deleted = false;  // 标记定时器项未删除
                return;
            }

            // 检查待添加的定时器是否已经存在
            if (_toAdd.TryGetValue(callback, out t))
            {
                t.set(interval, repeat, callback, callbackParam);  // 更新定时器设置
                return;
            }

            // 如果定时器项不存在，则从对象池中获取一个新的定时器项
            t = GetFromPool();
            t.interval = interval;  // 设置定时器间隔
            t.repeat = repeat;  // 设置定时器重复次数
            t.callback = callback;  // 设置定时器回调
            t.param = callbackParam;  // 设置回调参数
            _toAdd[callback] = t;  // 将定时器项添加到待添加字典中
        }

        // 延迟调用指定的回调，默认延迟0.001秒
        public void CallLater(TimerCallback callback)
        {
            Add(0.001f, 1, callback);  // 调用Add方法，设置延迟时间和重复次数
        }

        // 延迟调用指定的回调，带回调参数，默认延迟0.001秒
        public void CallLater(TimerCallback callback, object callbackParam)
        {
            Add(0.001f, 1, callback, callbackParam);  // 调用Add方法，设置延迟时间和重复次数
        }

        // 每帧调用指定回调，回调没有限制次数
        public void AddUpdate(TimerCallback callback)
        {
            Add(0.001f, 0, callback);  // 调用Add方法，设置延迟时间和无限次重复
        }

        // 每帧调用指定回调，回调带参数，且没有次数限制
        public void AddUpdate(TimerCallback callback, object callbackParam)
        {
            Add(0.001f, 0, callback, callbackParam);  // 调用Add方法，设置延迟时间和无限次重复
        }

        // 启动协程
        public void StartCoroutine(IEnumerator routine)
        {
            _engine.StartCoroutine(routine);  // 通过TimersEngine启动协程
        }

        // 检查某个回调的定时器是否存在
        public bool Exists(TimerCallback callback)
        {
            if (_toAdd.ContainsKey(callback))  // 如果回调在待添加的字典中
                return true;

            Anymous_T at;
            // 如果回调在已添加的字典中，且没有被删除
            if (_items.TryGetValue(callback, out at))
                return !at.deleted;

            return false;  // 否则返回false
        }

        // 移除指定回调的定时器
        public void Remove(TimerCallback callback)
        {
            Anymous_T t;
            // 如果回调在待添加字典中，移除并返回对象池
            if (_toAdd.TryGetValue(callback, out t))
            {
                _toAdd.Remove(callback);  // 从待添加字典中移除
                ReturnToPool(t);  // 将定时器项返回对象池
            }

            // 如果回调在已添加字典中，将其标记为已删除
            if (_items.TryGetValue(callback, out t))
                t.deleted = true;
        }

        // 从对象池中获取一个定时器项
        private Anymous_T GetFromPool()
        {
            Anymous_T t;
            int cnt = _pool.Count;  // 获取对象池中的项数
            if (cnt > 0)
            {
                // 如果池中有可用项，则取出并重置其状态
                t = _pool[cnt - 1];
                _pool.RemoveAt(cnt - 1);  // 从池中移除该项
                t.deleted = false;  // 标记未删除
                t.elapsed = 0;  // 重置已过去时间
            }
            else
                t = new Anymous_T();  // 如果池中没有项，则创建一个新的
            return t;
        }

        // 将定时器项返回对象池
        private void ReturnToPool(Anymous_T t)
        {
            t.callback = null;  // 清除回调
            _pool.Add(t);  // 将定时器项添加到对象池
        }

        // 每帧更新定时器的状态
        public void Update()
        {
            float dt = Time.unscaledDeltaTime;  // 获取无时间缩放的增量时间
            Dictionary<TimerCallback, Anymous_T>.Enumerator iter;

            // 如果存在已添加的定时器项
            if (_items.Count > 0)
            {
                iter = _items.GetEnumerator();
                while (iter.MoveNext())
                {
                    Anymous_T i = iter.Current.Value;  // 获取定时器项
                    if (i.deleted)  // 如果该定时器已删除，添加到待移除列表
                    {
                        _toRemove.Add(i);
                        continue;
                    }

                    i.elapsed += dt;  // 更新定时器的已过去时间
                    if (i.elapsed < i.interval)  // 如果未达到间隔时间，继续等待
                        continue;

                    i.elapsed -= i.interval;  // 重置已过去时间
                    if (i.elapsed < 0 || i.elapsed > 0.03f)  // 防止时间差异常
                        i.elapsed = 0;

                    if (i.repeat > 0)
                    {
                        i.repeat--;  // 减少重复次数
                        if (i.repeat == 0)  // 如果重复次数为0，标记为已删除
                        {
                            i.deleted = true;
                            _toRemove.Add(i);  // 将已删除的定时器添加到待移除列表
                        }
                    }

                    repeat = i.repeat;  // 更新当前剩余重复次数

                    // 执行回调
                    if (i.callback != null)
                    {
                        if (catchCallbackExceptions)
                        {
                            try
                            {
                                i.callback(i.param);  // 尝试执行回调
                            }
                            catch (System.Exception e)  // 捕获回调异常
                            {
                                i.deleted = true;  // 标记为已删除
                                Debug.LogWarning("FairyGUI: timer(internal=" + i.interval + ", repeat=" + i.repeat + ") callback error > " + e.Message);
                            }
                        }
                        else
                            i.callback(i.param);  // 执行回调
                    }
                }
                iter.Dispose();  // 释放枚举器
            }

            int len = _toRemove.Count;
            // 如果有待移除的定时器项
            if (len > 0)
            {
                for (int k = 0; k < len; k++)
                {
                    Anymous_T i = _toRemove[k];
                    if (i.deleted && i.callback != null)
                    {
                        _items.Remove(i.callback);  // 从已添加字典中移除回调
                        ReturnToPool(i);  // 将定时器项返回对象池
                    }
                }
                _toRemove.Clear();  // 清空待移除列表
            }

            // 将待添加的定时器项添加到已添加字典中
            if (_toAdd.Count > 0)
            {
                iter = _toAdd.GetEnumerator();
                while (iter.MoveNext())
                    _items.Add(iter.Current.Key, iter.Current.Value);  // 添加到已添加字典
                iter.Dispose();  // 释放枚举器
                _toAdd.Clear();  // 清空待添加列表
            }
        }
    }

    // 定时器项类，保存定时器的状态和回调信息
    class Anymous_T
    {
        public float interval;  // 定时器间隔
        public int repeat;  // 定时器重复次数
        public TimerCallback callback;  // 定时器回调
        public object param;  // 回调参数

        public float elapsed;  // 已过去的时间
        public bool deleted;  // 是否已删除

        // 设置定时器项的属性
        public void set(float interval, int repeat, TimerCallback callback, object param)
        {
            this.interval = interval;
            this.repeat = repeat;
            this.callback = callback;
            this.param = param;
        }
    }

    // 定时器引擎，负责定时器的更新
    class TimersEngine : MonoBehaviour
    {
        void Update()
        {
            Timers.inst.Update();  // 每帧调用Timers的Update方法
        }
    }
}
