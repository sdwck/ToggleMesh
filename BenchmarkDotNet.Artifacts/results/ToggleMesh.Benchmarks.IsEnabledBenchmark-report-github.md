```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.5624/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  

```
| Method                            | Mean       | Error     | StdDev    | Max        | Min        | P95        | Allocated |
|---------------------------------- |-----------:|----------:|----------:|-----------:|-----------:|-----------:|----------:|
| Evaluate_1Rule_TypedContext       |  66.778 ns | 0.1967 ns | 0.1840 ns |  67.063 ns |  66.513 ns |  67.016 ns |         - |
| Evaluate_1Rule_AOT                |  28.199 ns | 0.1186 ns | 0.1110 ns |  28.341 ns |  28.041 ns |  28.334 ns |         - |
| Evaluate_1Rule_Dictionary         | 104.651 ns | 0.8339 ns | 0.7801 ns | 105.987 ns | 103.395 ns | 105.874 ns |         - |
| Evaluate_ComplexRule_TypedContext | 142.067 ns | 0.5647 ns | 0.5282 ns | 142.886 ns | 141.152 ns | 142.730 ns |         - |
| Evaluate_ComplexRule_AOT          |  94.317 ns | 0.5513 ns | 0.5157 ns |  95.085 ns |  93.332 ns |  95.078 ns |         - |
| Evaluate_ComplexRule_Dictionary   | 190.116 ns | 0.7678 ns | 0.7182 ns | 190.991 ns | 188.717 ns | 190.985 ns |         - |
| Evaluate_10Rules_AOT              | 121.137 ns | 0.7390 ns | 0.6913 ns | 121.929 ns | 119.772 ns | 121.911 ns |         - |
| Evaluate_NoRules_AOT              |   7.044 ns | 0.0399 ns | 0.0373 ns |   7.111 ns |   6.995 ns |   7.102 ns |         - |
| Evaluate_50_50_Rollout_AOT        |  38.030 ns | 0.1075 ns | 0.1006 ns |  38.213 ns |  37.873 ns |  38.206 ns |         - |
| GetJsonVariation                  |  21.990 ns | 0.0386 ns | 0.0342 ns |  22.036 ns |  21.917 ns |  22.034 ns |         - |
| GetJsonVariation_WithUser_AOT     |  27.400 ns | 0.1458 ns | 0.1364 ns |  27.596 ns |  27.254 ns |  27.592 ns |         - |
| GetStringVariation                |  23.476 ns | 0.1152 ns | 0.1077 ns |  23.613 ns |  23.328 ns |  23.603 ns |         - |
| GetStringVariation_WithUser_AOT   |  27.397 ns | 0.1520 ns | 0.1421 ns |  27.541 ns |  27.071 ns |  27.536 ns |         - |
| Analytics_TrackEvent_Simple       |  44.592 ns | 0.2763 ns | 0.2585 ns |  45.017 ns |  44.213 ns |  44.993 ns |         - |
| Analytics_TrackEvent_10Rules_AOT  |  46.218 ns | 0.2303 ns | 0.2154 ns |  46.607 ns |  45.818 ns |  46.540 ns |         - |
