﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
/// <summary>
/// 指令集
/// </summary>
namespace ZeptLua.ISA
{
    /// <summary>
    /// byte code instruction，没有搞清楚sbx的问题。另外我删除了RK机制，不使用这种编码 => 真香
    /// </summary>
    [Serializable]
    struct Bytecode : ITest
    {
        uint i;
        #region ctor and factorys
        /// <summary>
        /// 实现决策】struct本身语义就是与int相等，因此只允许使用cast
        /// </summary>
        Bytecode(uint i)
        {
            this.i = i;
        }
        public Bytecode(Op opcode, int a, int b, int c)
        {
            uint A = (uint)a;
            uint B = (uint)b;
            uint C = (uint)c;
            Debug.Assert(A < (1 << 8));
            Debug.Assert(B < (1 << 9));
            Debug.Assert(C < (1 << 9));
            i = (uint)opcode << PosOp | A << PosA | B << PosB | C << PosC;
        }
        public Bytecode(Op opcode, int a, int bx)
        {
            uint A = (uint)a;
            uint Bx = (uint)bx;
            Debug.Assert(A < (1 << 8));
            Debug.Assert(Bx < (1 << 18));
            i = (uint)opcode << PosOp | A << PosA | Bx << PosBx;
        }
        /// <summary>
        /// 基于ctor的工厂，因为RA RKB RKC这种指令格式最好有通用函数，opcode会被检查
        /// bool中K是true，R是false
        /// 设计决策】没有办法提供很好的设计。所以只能提供这种简单的包装的辅助函数
        /// </summary>
        public static Bytecode RaRKbRkc(Op opcode, int a, bool isKorR1, int b, bool isKorR2, int c)
        {
            //检查opcode范围
            Debug.Assert((int)opcode >= (int)Op.Add && (int)opcode <= (int)Op.Le);
            Debug.Assert(!((int)opcode >= (int)Op.Unm && (int)opcode <= (int)Op.Jmp));
            int rkb = b | (Convert.ToInt32(isKorR1) << 8);
            int rkc = c | (Convert.ToInt32(isKorR2) << 8);
            return new Bytecode(opcode, a, rkb, rkc);
        }
        /// <summary>
        /// loadnil, move, unm, not, len,  return, get/setupval
        /// </summary>
        public static Bytecode RaRb(Op opcode, int a, int b)
        {
            return new Bytecode(opcode, a, b, 0);
        }
        /// <summary>
        /// local a, b = 10; b = a
        /// 1. 为函数调用，get/setTable，concat移动oprd，因为栈顺序重要
        /// 2. 为函数返回值移动到局部变量
        /// 3. 作为closure后的伪指令
        /// </summary>
        public static Bytecode Mov(int a, int b) => RaRb(Op.Move, a, b);
        public static Bytecode LoadNil(int a, int b) => RaRb(Op.LoadNil, a, b);
        public static Bytecode LoadN(int a, int bx) => new Bytecode(Op.LoadN, a, bx);
        public static Bytecode LoadS(int a, int bx) => new Bytecode(Op.LoadS, a, bx);
        /// <summary>
        /// RA = B, if C then pc++
        /// </summary>
        public static Bytecode LoadB(int a, bool b, bool c = false) =>
            new Bytecode(Op.LoadBool, a, Convert.ToInt32(b), Convert.ToInt32(c));
        public static Bytecode GetG(int a, int bx) => new Bytecode(Op.GetGlobal, a, bx);
        public static Bytecode SetG(int a, int bx) => new Bytecode(Op.SetGlobal, a, bx);
        public static Bytecode GetU(int a, int b) => RaRb(Op.GetUpVal, a, b);
        public static Bytecode SetU(int a, int b) => RaRb(Op.SetUpval, a, b);
        public static Bytecode GetL(int a, int bx) => new Bytecode(Op.GetLocal, a, bx);
        public static Bytecode SetL(int a, int bx) => new Bytecode(Op.SetLocal, a, bx);
        /// <summary>
        /// 一个仅用于warp args的struct
        /// </summary>
        internal struct RK
        {
            public bool isK;
            public int val;
            public RK(bool isK, int val)
            {
                this.isK = isK;
                this.val = val;
            }
        }
        /// <summary>
        /// RA = RB[RKC]
        /// </summary>
        public static Bytecode GetTable(int a, int b, RK rkc)
        {
            int c = rkc.val | (Convert.ToInt32(rkc.isK) << 8);
            return new Bytecode(Op.GetTable, a, b, c);
        }
        /// <summary>
        /// RA[RKB] = RKC
        /// </summary>
        public static Bytecode SetTable(int a, RK rkb, RK rkc)
        {
            int b = rkb.val | (Convert.ToInt32(rkb.isK) << 8);
            int c = rkc.val | (Convert.ToInt32(rkc.isK) << 8);
            return new Bytecode(Op.SetTable, a, b, c);
        }
        public static Bytecode Unm(int a, int b) => RaRb(Op.Unm, a, b);
        public static Bytecode Not(int a, int b) => RaRb(Op.Not, a, b);
        public static Bytecode Len(int a, int b) => RaRb(Op.Len, a, b);
        /// <summary>
        /// RA = String.Join(RB, ..., RC)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static Bytecode Concat(int a, int b, int c) => new Bytecode(Op.Concat, a, b, c);
        public static Bytecode Jmp(int sbx) => new Bytecode(Op.Jmp, 0, sbx);
        public static Bytecode Call(int a, int b, int c) => new Bytecode(Op.Call, a, b, c);
        /// <summary>
        /// return A  => mov RA, ...,R[L.topIndex] to R[ci.funcIndex], ...
        /// 我修改了指令和函数调用的语义。现在不需要RB了
        /// </summary>
        public static Bytecode Ret(int a) => new Bytecode(Op.Return, a, 0, 0);
        /// <summary>
        /// RA, RA+1, ...,RA+B-2 = vararg 
        /// </summary>
        public static Bytecode Vararg(int a, int b) => RaRb(Op.VarArg, a, b);
        /// <summary>
        /// RA+1 = RB, RA = RB[RKC]: load function from table RB to RA, put table RB iteself at RA+1 as the first arg 
        /// </summary>
        public static Bytecode Self(int a, int b, int c) => new Bytecode(Op.Self, a, b, c);
        #endregion
        #region getters, setters
        public Op Opcode
        {
            get => (Op)Get(SizeOp, PosOp);
            set => Set((int)value, SizeOp, PosOp);
        }
        public int A
        {
            get => Get(SizeA, PosA);
            set => Set(value, SizeA, PosA);
        }
        public int B
        {
            get => Get(SizeB, PosB);
            set => Set(value, SizeB, PosB);
        }
        public int C
        {
            get => Get(SizeC, PosC);
            set => Set(value, SizeC, PosC);
        }
        public int Bx
        {
            get => Get(SizeBx, PosBx);
            set => Set(value, SizeBx, PosBx);
        }
        /// <summary>
        /// sBx
        /// </summary>
        public int SignedBx
        {
            get => Bx - MaxArgSignedBx;
            set => Bx = value + MaxArgSignedBx;
        }
        #endregion
        #region handle RK
        /// <summary>
        /// if x[7] (in bit) is 1, return true, and RKB returns KB
        /// </summary>
        /// <param name="x">B or C</param>
        public static bool IsK(int x) => (x & (1 << (SizeB - 1))) != 0;
        public static int IndexK(int x) => x & ~(1 << (SizeB - 1));
        internal static List<Bytecode> Gen(uint[] hexs)
        {
            var a = new List<Bytecode>();
            foreach (var item in hexs) {
                a.Add(new Bytecode(item));
            }
            return a;
        }
        #endregion
        public static explicit operator uint(Bytecode i) => i.i;
        public static explicit operator Bytecode(uint i) => new Bytecode(i);
        #region private things
        #region size and position of instruction arguments
        const int SizeC = 9;
        const int SizeB = 9;
        const int SizeBx = SizeC + SizeB;
        const int SizeA = 8;
        const int SizeOp = 6;

        const int PosOp = 0;
        const int PosA = PosOp + SizeOp;
        const int PosC = PosA + SizeA;
        const int PosB = PosC + SizeC;
        const int PosBx = PosC;
        #endregion
        #region max size of instrucion arguments
        const int MaxArgBx = (1 << SizeBx) - 1; // 1. "<<" is less prior to "-" 2. "1<<n" = 2**n 
        const int MaxArgSignedBx = MaxArgBx >> 1;
        const int MaxArgA = (1 << SizeA) - 1;
        const int MaxArgB = (1 << SizeB) - 1;
        const int MaxArgC = (1 << SizeC) - 1;
        #endregion
        #region getter and setter of instruction
        static uint Mask1(int n, int pos) => (~((~(uint)0) << n)) << pos;
        static uint Mask0(int n, int pos) => ~Mask0(n, pos);
        int Get(int n, int pos)
        {
            var a = (i >> pos);
            var b = Mask1(n, 0);
            return (int)(a & b);
        }
        void Set(int x, int n, int pos)
        {
            var a = i & Mask0(n, pos);
            var b = ((uint)x << pos) & Mask1(n, pos); // "(uint)"是必要的。
            i = a | b;
        }
        #endregion
        #endregion

        public override string ToString()
        {
            return Opcode.ToString() + " A: " + A.ToString() +
               " B: " + B.ToString() + " C: " + C.ToString() +
               " Bx: " + Bx.ToString();
        }
        public void Test()
        {
            void TestCreateOprd2()
            {
                Assert.AreEqual<UInt32>(0x00000007, (uint)new Bytecode(Op.SetGlobal, 0, 0));
                Assert.AreEqual<UInt32>(0x00004007, (uint)new Bytecode(Op.SetGlobal, 0, 1));
                Assert.AreEqual<UInt32>(0x00008007, (uint)new Bytecode(Op.SetGlobal, 0, 2));
                Assert.AreEqual<UInt32>(0x00010001, (uint)new Bytecode(Op.LoadK, 0, 4));
                Assert.AreEqual<UInt32>(0x0000C007, (uint)new Bytecode(Op.SetGlobal, 0, 3));
                Assert.AreEqual<UInt32>(0x00018001, (uint)new Bytecode(Op.LoadK, 0, 6));
                Assert.AreEqual<UInt32>(0x00014007, (uint)new Bytecode(Op.SetGlobal, 0, 5));
                Assert.AreEqual<UInt32>(0x00020001, (uint)new Bytecode(Op.LoadK, 0, 8));
                Assert.AreEqual<UInt32>(0x0001C007, (uint)new Bytecode(Op.SetGlobal, 0, 7));
                Assert.AreEqual<UInt32>(0x00000003, (uint)new Bytecode(Op.LoadNil, 0, 0));
                Assert.AreEqual<UInt32>(0x000100C1, (uint)new Bytecode(Op.LoadK, 3, 4));
                Assert.AreEqual<UInt32>(0x00018101, (uint)new Bytecode(Op.LoadK, 4, 6));
                Assert.AreEqual<UInt32>(0x00020141, (uint)new Bytecode(Op.LoadK, 5, 8));
                Assert.AreEqual<UInt32>(0x000101C1, (uint)new Bytecode(Op.LoadK, 7, 4));
                Assert.AreEqual<UInt32>(0x00024201, (uint)new Bytecode(Op.LoadK, 8, 9));
                Assert.AreEqual<UInt32>(0x000241C1, (uint)new Bytecode(Op.LoadK, 7, 9));
                Assert.AreEqual<UInt32>(0x00028201, (uint)new Bytecode(Op.LoadK, 8, 10));
                Assert.AreEqual<UInt32>(0x00000324, (uint)new Bytecode(Op.Closure, 12, 0));
                Assert.AreEqual<UInt32>(0x00034307, (uint)new Bytecode(Op.SetGlobal, 12, 13));
                Assert.AreEqual<UInt32>(0x00034305, (uint)new Bytecode(Op.GetGlobal, 12, 13));
                Assert.AreEqual<UInt32>(0x00010341, (uint)new Bytecode(Op.LoadK, 13, 4));
                Assert.AreEqual<UInt32>(0x00024381, (uint)new Bytecode(Op.LoadK, 14, 9));
            }
            void TestCreateOprd3()
            {

                Assert.AreEqual<UInt32>(0x00000002, (uint)new Bytecode(Op.LoadBool, 0, 0, 0));
                Assert.AreEqual<UInt32>(0x00800002, (uint)new Bytecode(Op.LoadBool, 0, 1, 0));
                Assert.AreEqual<UInt32>(0x00000042, (uint)new Bytecode(Op.LoadBool, 1, 0, 0));
                Assert.AreEqual<UInt32>(0x00800082, (uint)new Bytecode(Op.LoadBool, 2, 1, 0));
                Assert.AreEqual<UInt32>(0x0100018A, (uint)new Bytecode(Op.NewTable, 6, 2, 0));
                Assert.AreEqual<UInt32>(0x010041A2, (uint)new Bytecode(Op.SetList, 6, 2, 1));
                Assert.AreEqual<UInt32>(0x85C3024C, (uint)new Bytecode(Op.Add, 9, 267, 268));
                Assert.AreEqual<UInt32>(0x82424057, (uint)new Bytecode(Op.Eq, 1, 260, 265));
                Assert.AreEqual<UInt32>(0x00004282, (uint)new Bytecode(Op.LoadBool, 10, 0, 1));
                Assert.AreEqual<UInt32>(0x00800282, (uint)new Bytecode(Op.LoadBool, 10, 1, 0));
                Assert.AreEqual<UInt32>(0x000002C2, (uint)new Bytecode(Op.LoadBool, 11, 0, 0));
                Assert.AreEqual<UInt32>(0x0180831C, (uint)new Bytecode(Op.Call, 12, 3, 2));

                // TODO，这两条指令的C是省略为0的，另外jmp型是sbx，还没有考虑
                Assert.AreEqual<UInt32>(0x03000194, (uint)new Bytecode(Op.Len, 6, 6, 0));
                Assert.AreEqual<UInt32>(0x0080001E, (uint)new Bytecode(Op.Return, 0, 1, 0));


            }
            void TestA()
            {
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 0, 0, 0).A);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 0, 1, 0).A);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.LoadBool, 1, 0, 0).A);
                Assert.AreEqual<Int32>(2, new Bytecode(Op.LoadBool, 2, 1, 0).A);
                Assert.AreEqual<Int32>(6, new Bytecode(Op.NewTable, 6, 2, 0).A);
                Assert.AreEqual<Int32>(6, new Bytecode(Op.SetList, 6, 2, 1).A);
                Assert.AreEqual<Int32>(9, new Bytecode(Op.Add, 9, 267, 268).A);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.Eq, 1, 260, 265).A);
                Assert.AreEqual<Int32>(10, new Bytecode(Op.LoadBool, 10, 1, 0).A);
                Assert.AreEqual<Int32>(11, new Bytecode(Op.LoadBool, 11, 0, 0).A);
                Assert.AreEqual<Int32>(12, new Bytecode(Op.Call, 12, 3, 2).A);
            }
            void TestB()
            {
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 0, 0, 0).B);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.LoadBool, 0, 1, 0).B);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 1, 0, 0).B);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.LoadBool, 2, 1, 0).B);
                Assert.AreEqual<Int32>(2, new Bytecode(Op.NewTable, 6, 2, 0).B);
                Assert.AreEqual<Int32>(2, new Bytecode(Op.SetList, 6, 2, 1).B);
                Assert.AreEqual<Int32>(267, new Bytecode(Op.Add, 9, 267, 268).B);
                Assert.AreEqual<Int32>(260, new Bytecode(Op.Eq, 1, 260, 265).B);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.LoadBool, 10, 1, 0).B);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 11, 0, 0).B);
                Assert.AreEqual<Int32>(3, new Bytecode(Op.Call, 12, 3, 2).B);
            }
            void TestC()
            {
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 0, 0, 0).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 0, 1, 0).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 1, 0, 0).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 2, 1, 0).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.NewTable, 6, 2, 0).C);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.SetList, 6, 2, 1).C);
                Assert.AreEqual<Int32>(268, new Bytecode(Op.Add, 9, 267, 268).C);
                Assert.AreEqual<Int32>(265, new Bytecode(Op.Eq, 1, 260, 265).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 10, 1, 0).C);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadBool, 11, 0, 0).C);
                Assert.AreEqual<Int32>(2, new Bytecode(Op.Call, 12, 3, 2).C);
            }
            void TestBx()
            {
                Assert.AreEqual<Int32>(0, new Bytecode(Op.SetGlobal, 0, 0).Bx);
                Assert.AreEqual<Int32>(1, new Bytecode(Op.SetGlobal, 0, 1).Bx);
                Assert.AreEqual<Int32>(2, new Bytecode(Op.SetGlobal, 0, 2).Bx);
                Assert.AreEqual<Int32>(4, new Bytecode(Op.LoadK, 0, 4).Bx);
                Assert.AreEqual<Int32>(3, new Bytecode(Op.SetGlobal, 0, 3).Bx);
                Assert.AreEqual<Int32>(6, new Bytecode(Op.LoadK, 0, 6).Bx);
                Assert.AreEqual<Int32>(5, new Bytecode(Op.SetGlobal, 0, 5).Bx);
                Assert.AreEqual<Int32>(8, new Bytecode(Op.LoadK, 0, 8).Bx);
                Assert.AreEqual<Int32>(7, new Bytecode(Op.SetGlobal, 0, 7).Bx);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.LoadNil, 0, 0).Bx);
                Assert.AreEqual<Int32>(4, new Bytecode(Op.LoadK, 3, 4).Bx);
                Assert.AreEqual<Int32>(6, new Bytecode(Op.LoadK, 4, 6).Bx);
                Assert.AreEqual<Int32>(8, new Bytecode(Op.LoadK, 5, 8).Bx);
                Assert.AreEqual<Int32>(4, new Bytecode(Op.LoadK, 7, 4).Bx);
                Assert.AreEqual<Int32>(9, new Bytecode(Op.LoadK, 8, 9).Bx);
                Assert.AreEqual<Int32>(9, new Bytecode(Op.LoadK, 7, 9).Bx);
                Assert.AreEqual<Int32>(10, new Bytecode(Op.LoadK, 8, 10).Bx);
                Assert.AreEqual<Int32>(0, new Bytecode(Op.Closure, 12, 0).Bx);
                Assert.AreEqual<Int32>(13, new Bytecode(Op.SetGlobal, 12, 13).Bx);
                Assert.AreEqual<Int32>(13, new Bytecode(Op.GetGlobal, 12, 13).Bx);
                Assert.AreEqual<Int32>(4, new Bytecode(Op.LoadK, 13, 4).Bx);
                Assert.AreEqual<Int32>(9, new Bytecode(Op.LoadK, 14, 9).Bx);
            }
            TestCreateOprd2();
            TestCreateOprd3();
            TestA();
            TestB();
            TestC();
            TestBx();
        }
    }

    enum Op
    {
        /* mov类*/
        Move,
        LoadK,
        LoadBool,
        LoadNil,
        GetUpVal,
        GetGlobal,
        GetTable,
        SetGlobal,
        SetUpval,
        SetTable,

        NewTable,
        Self,
        /* 算术运算类*/
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Pow,
        Unm,
        /* 关系运算 逻辑运算 jmp*/
        Not,
        Len,
        Concat,
        Jmp,
        Eq,
        Lt,
        Le,
        Test,
        Testset,
        /* 调用*/
        Call,
        //TailCall,
        Return,
        /* 循环*/
        ForLoop,
        ForPrep,
        TForLoop,
        /* 特殊，最后补上即可*/
        SetList,

        Close,
        Closure,
        /* 特殊，最后补上即可*/
        VarArg,
        /*自定增加的指令*/
        LoadN, //loadN和loadS替代LoadK
        LoadS,
        Ne, //不加lparser里没法实现
        GetLocal,  //额外加的指令，简化实现
        SetLocal,
    }
}