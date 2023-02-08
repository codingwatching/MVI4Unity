﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace MVI4Unity
{
    public class PoolMgr : SafeSingleton<PoolMgr>
    {
        private readonly Dictionary<Type , IPoolType> _poolTypeDict = new Dictionary<Type , IPoolType> ();
        private readonly Dictionary<IPoolType , IPoolStorage> _storageDict = new Dictionary<IPoolType , IPoolStorage> ();

        /// <summary>
        /// 池中弹出
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="poolType"></param>
        /// <returns></returns> 
        public T Pop<T> (IPoolType poolType)
        {
            var storage = GetStorage<T> (poolType);
            if ( storage.GetStoragedCount () > 0 )
            {
                return ( T ) storage.Pop ();
            }
            else
            {
                return ( T ) poolType.Create ();
            }
        }

        /// <summary>
        /// 回收
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="poolType"></param>
        /// <param name="item"></param>
        public void Push<T> (PoolType<T> poolType , T item)
        {
            GetStorage<T> (poolType).Push (item);
        }

        /// <summary>
        /// 尝试获取一个对象池类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pooltype"></param>
        /// <returns></returns>
        public bool TryGetPoolType<T> (out IPoolType pooltype)
        {
            return _poolTypeDict.TryGetValue (typeof (T) , out pooltype);
        }

        /// <summary>
        /// 获取实际存储的仓库
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="poolType"></param>
        /// <returns></returns>
        private IPoolStorage GetStorage<T> (IPoolType poolType)
        {
            if ( !_storageDict.ContainsKey (poolType) )
            {
                _storageDict.Add (poolType , new PoolStorage<T> (poolType));
            }
            return _storageDict [poolType];
        }

        #region 集合对象池，集合实例回收利用，进行Clear而不是重新New，减少GC，比如List，Stack等都是一个道理

        /// <summary>
        /// 针对List的池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PoolType<List<T>> GetList<T> ()
        {
            if ( !_poolTypeDict.ContainsKey (typeof (List<T>)) )
            {
                _poolTypeDict [typeof (List<T>)] = new PoolType<List<T>> (
                    onCreate: () =>
                    {
                        return new List<T> (0);
                    } ,
                    onPush: (t) =>
                    {
                        t.Clear ();
                    });
            }
            return _poolTypeDict [typeof (List<T>)] as PoolType<List<T>>;
        }

        /// <summary>
        /// 针对Dict的池
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public PoolType<Dictionary<T1 , T2>> GetDict<T1, T2> ()
        {
            if ( !_poolTypeDict.ContainsKey (typeof (Dictionary<T1 , T2>)) )
            {
                _poolTypeDict [typeof (Dictionary<T1 , T2>)] = new PoolType<Dictionary<T1 , T2>> (
                    onCreate: () =>
                    {
                        return new Dictionary<T1 , T2> ();
                    } ,
                    onPush: (t) =>
                    {
                        t.Clear ();
                    });
            }
            return _poolTypeDict [typeof (Dictionary<T1 , T2>)] as PoolType<Dictionary<T1 , T2>>;
        }

        #endregion

        #region AWindow对象池

        /// <summary>
        /// 弹出窗口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="windowPath"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public AWindow PopAWindow<T> (string windowPath , Transform parent) where T : AWindow
        {
            return GetAWindowPool<T> (windowPath , parent).Pop ();
        }

        /// <summary>
        /// 回收窗口到池里
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="window"></param>
        public void PushAWindow<T> (T window) where T : AWindow
        {
            if ( _poolTypeDict.TryGetValue (typeof (T) , out var poolType) )
            {
                ( poolType as PoolType<T> ).Push (window);
            }
        }

        /// <summary>
        /// 获取窗口对象池
        /// </summary>
        /// <typeparam name="T">该泛型类型被限制为AWindow</typeparam>
        /// <param name="windowPath">窗口预制体路径</param>
        /// <param name="parent">父节点</param>
        /// <returns></returns>
        public PoolType<T> GetAWindowPool<T> (string windowPath , Transform parent) where T : AWindow
        {
            Type windowType = typeof (T);
            if ( !_poolTypeDict.ContainsKey (windowType) )
            {
                _poolTypeDict [windowType] = new PoolType<T> (
                    onCreate: () =>
                    {
                        return UIWinMgr.Ins.Create<T> (windowPath , parent);
                    } ,
                    onPop: default ,
                    onPush: OnAWindowPush);
            }
            return _poolTypeDict [windowType] as PoolType<T>;
        }

        /// <summary>
        /// 获取窗口对象池
        /// </summary>
        /// <typeparam name="T">该泛型类型被限制为AWindow</typeparam>
        /// <param name="windowPath">窗口预制体路径</param>
        /// <param name="parent">父节点</param>
        /// <param name="onCreate">创建方式（替换默认方式）</param>
        /// <param name="onPop">弹出监听</param>
        /// <param name="onPush">回收监听</param>
        /// <returns></returns>
        public PoolType<T> GetAWindowPool<T> (string windowPath , Transform parent , Func<T> onCreate = null , Action onPop = null , Action<T> onPush = null) where T : AWindow
        {
            Type windowType = typeof (T);
            onCreate = onCreate ?? ( () => { return UIWinMgr.Ins.Create<T> (windowPath , parent); } );
            if ( !_poolTypeDict.ContainsKey (windowType) )
            {
                _poolTypeDict [windowType] = new PoolType<T> (
                    onCreate: onCreate ,
                    onPop: onPop ,
                    onPush: (t) =>
                    {
                        OnAWindowPush (t);
                        onPush?.Invoke (t);
                    });
            }
            return _poolTypeDict [windowType] as PoolType<T>;
        }

        /// <summary>
        /// 当窗口回收时
        /// </summary>
        /// <param name="window"></param>
        void OnAWindowPush (AWindow window)
        {

        }

        #endregion

        #region WindowNode对象池

        /// <summary>
        /// 获取窗口节点对象池
        /// </summary>
        /// <returns></returns>
        public PoolType<WindowNode> GetWindowNodePool ()
        {
            Type type = typeof (WindowNode);
            if ( !_poolTypeDict.ContainsKey (typeof (WindowNode)) )
            {
                _poolTypeDict [type] = new PoolType<WindowNode> (
                    onCreate: () => { return new WindowNode (); } ,
                    onPop: default ,
                    onPush: default);
            }
            return _poolTypeDict [type] as PoolType<WindowNode>;
        }

        #endregion
    }
}