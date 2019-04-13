﻿using System;

namespace zlua.Core.Lua
{
    // lua整数类型
    //
    // 不支持配置成比如int32
    public struct lua_Integer : IEquatable<lua_Integer>
    {
        public Int64 Value { get; set; }

        public override bool Equals(object obj)
        {
            return obj is lua_Integer && Equals((lua_Integer)obj);
        }

        public bool Equals(lua_Integer other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            var hashCode = -783812246;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + Value.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(lua_Integer i1, lua_Integer i2)
        {
            return i1.Equals(i2);
        }

        public static bool operator !=(lua_Integer i1, lua_Integer i2)
        {
            return !i1.Equals(i2);
        }

        public static implicit operator lua_Integer(Int64 i)
        {
            return new lua_Integer { Value = i };
        }

        public static implicit operator Int64(lua_Integer i)
        {
            return i.Value;
        }

        public static explicit operator lua_Integer(lua_Number n)
        {
            return (Int64)n;
        }

        // TODO，先进行词法验证，luanumber也是，lua和c#标准不同
        public static bool TryParse(string s, out lua_Integer i)
        {
            Int64 outI;
            bool b;
            b = Int64.TryParse(s, out outI);
            i = outI;
            return b;
        }
    }
}