﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zlua.TypeModel;
using zlua.VM;

namespace zlua.GlobalState
{
    /* <lua_src>struct global_State;</lua_src>*/
    public class GlobalState
    {
        public TValue registery;
        public TThread mainThread;
    }
}