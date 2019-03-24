# zlua_prototype

zlua的python原型

相比C#版，原型已经实现得相对完整
* 基本能执行大部分lua代码，可以参考测试用例中的lua代码
* 实现了与宿主语言的互操作，zlua和python之间可以非常方便地传递函数
* 由于lua的元表机制，自带OOP系统
* 实现了一个简单的调试器，运行时使用命令添加断点，可以停止代码查看变量的值

我认为lua的c源代码实现的问题如下：
* 源代码很不符合软件工程，导致非常难读
  * 缺少解释
  * 变量名都是缩写
  * 调试器挂钩等附属内容混在代码中
* 源代码的栈非常深，从main到执行指令循环可能有10几次函数调用

我认为上面的问题是因为lua的设计目标是性能，它特别关注性能，lua的实现要小，要快，还得是c语言

然而这会限制发展，导致很多团队或个人版本的lua产生

lua语言的设计问题如下：
* 类型系统有问题，字符串和数字类型不应该允许自动转换
* 没有标准的OOP实现，每个团队自己写一个Class方法
* 变量声明默认是全局的而不是局部的，python正好相反，我想这应该不是问题

这些是完全可以做好的
