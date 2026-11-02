using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// 有关程序集的一般信息由以下
// 控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyTitle("KeyboardOG")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("KeyboardOG")]
[assembly: AssemblyCopyright("Copyright © LGGZ 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 会使此程序集中的类型
//对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型
//请将此类型的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("71440872-c557-46d3-958b-6c81e657ce48")]

// 程序集的版本信息由下列四个值组成: 
//
//      主版本
//      次版本
//      生成号
//      修订号
//
// 版本编码是固件那套 2|3|3（见 Protocol/VersionInfo.cs 的 AppVersion.FromAssembly）：
//   次版本 = Minor1（0~7）、生成号 = Minor2（0~7），所以“1.11”要写成 1.1.1.0。
//   写成 1.11.0.0 会被 & 0x07 截成 Minor1=3 → 显示成 1.30（1.10.0.0 当年同样错成 1.20）。
[assembly: AssemblyVersion("1.1.1.0")]
// 文件属性里给人看的版本号，可以按常规写法写
[assembly: AssemblyFileVersion("1.11.0.0")]
