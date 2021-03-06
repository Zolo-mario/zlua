﻿using System.Diagnostics;
using ZoloLua.Core.Lua;
using ZoloLua.Core.MetaMethod;
using ZoloLua.Core.TypeModel;

namespace ZoloLua.Core.VirtualMachine
{
    public partial class lua_State
    {
        private static string[] luaT_eventname { get; } =
        {
            "__index", "__newindex",
            "__gc", "__mode", "__eq",
            "__add", "__sub", "__mul", "__div", "__mod",
            "__pow", "__unm", "__len", "__lt", "__le",
            "__concat", "__call"
        };

        /// ; result = __add(lhs, rhs); __add从lhs和rhs的元表查找出，如果都没找到返回false
        private bool call_binTM(TValue p1, TValue p2, TValue res /*result*/, TMS @event)
        {
            res = null;
            TValue tm = luaT_gettmbyobj(p1, @event);
            if (tm.IsNil) {
                tm = luaT_gettmbyobj(p2, @event);
            }
            if (tm.IsNil) {
                return false;
            }
            callTMres(res, tm, p1, p2);
            return true;
        }

        // 这里会返回-1,0,1
        // -1是失败标志，否则返回0或1作为bool值
        private int call_orderTM(TValue p1, TValue p2,
                                 TMS @event)
        {
            TValue tm1 = luaT_gettmbyobj(p1, @event);
            TValue tm2;
            if (tm1.IsNil) {
                return -1; /* no metamethod? */
            }
            tm2 = luaT_gettmbyobj(p2, @event);
            if (!lobject.luaO_rawequalObj(tm1, tm2)) /* different metamethods? */ {
                return -1;
            }
            callTMres(top, tm1, p1, p2);
            return !stack[top.index].IsFalse ? 1 : 0;
        }

        private void callTM(TValue f, TValue p1, TValue p2, TValue p3)
        {
            push(f);
            push(p1);
            push(p2);
            push(p3);
            luaD_call(top - 4, 0);
        }

        /// <summary>
        ///     <paramref name="res" />=<paramref name="f" />(<paramref name="p1" />,<paramref name="p2" />)
        /// </summary>
        /// <param name="res"></param>
        /// <param name="f"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        private void callTMres(TValue res, TValue f, TValue p1, TValue p2)
        {
            push(f);
            push(p1);
            push(p2);
            luaD_call(top - 3, 1);
            top--;
            res.Value = top;
        }

        private TValue fasttm(Table et, TMS e)
        {
            return et == null ?
                null :
                (et.flags & (1 << (int)e)) != 0 ?
                    null :
                    luaT_gettm(et, e, G.tmname[(int)e]);
        }

        private TValue get_compTM(Table mt1, Table mt2,
                                  TMS @event)
        {
            TValue tm1 = fasttm(mt1, @event);
            TValue tm2;
            if (tm1 == null) {
                return null; /* no metamethod */
            }
            if (mt1 == mt2) {
                return tm1; /* same metatables => same metamethods */
            }
            tm2 = fasttm(mt2, @event);
            if (tm2 == null) {
                return null; /* no metamethod */
            }
            if (lobject.luaO_rawequalObj(tm1, tm2)) /* same metamethods? */ {
                return tm1;
            }
            return null;
        }

        /// <summary>
        ///     function to be used with macro "fasttm": optimized for absence of tag methods
        /// </summary>
        /// <param name="events"></param>
        /// <param name="event"></param>
        /// <param name="ename"></param>
        /// <returns></returns>
        private static TValue luaT_gettm( /*this*/ Table events, TMS @event, TString ename)
        {
            TValue tm = events.luaH_getstr(ename);
            Debug.Assert((int)@event <= (int)TMS.TM_EQ);
            if (tm.IsNil) { /* no tag method? */
                events.flags |= (byte)(1 << (int)@event); /* cache this fact */
                return null;
            }
            return tm;
        }

        /// <summary>
        ///     get metamethod from `obj，
        ///     obj没有元表或没有该元方法返回nilobject；enum和string一一对应，这里从enum开始
        /// </summary>
        private TValue luaT_gettmbyobj(TValue o, TMS @event)
        {
            Table mt;
            switch (o.tt) {
                case LuaType.LUA_TTABLE:
                    mt = o.Table.metatable;
                    break;
                //case LuaType.Udata:
                //    metatable = obj.Udata.metaTable;
                //    break;
                default:
                    mt = G.mt[(int)o.tt];
                    break;
            }
            return mt != null ?
                mt.luaH_getstr(G.tmname[(int)@event]) :
                lobject.luaO_nilobject;
        }

        private void luaT_init()
        {
            for (int i = 0; i < (int)TMS.TM_N; i++) G.tmname[i] = new TString(luaT_eventname[i]);
        }
    }
}