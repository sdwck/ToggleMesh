```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.5624/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  

```
| Method                            | Mean       | Error     | StdDev    | Max        | Min        | P95        | Allocated |
|---------------------------------- |-----------:|----------:|----------:|-----------:|-----------:|-----------:|----------:|
| Evaluate_1Rule_TypedContext       |  67.291 ns | 0.2226 ns | 0.2082 ns |  67.613 ns |  66.931 ns |  67.587 ns |         - |
| Evaluate_1Rule_AOT                |  28.133 ns | 0.0855 ns | 0.0758 ns |  28.285 ns |  28.030 ns |  28.242 ns |         - |
| Evaluate_1Rule_Dictionary         | 106.321 ns | 0.2934 ns | 0.2744 ns | 106.829 ns | 105.942 ns | 106.802 ns |         - |
| Evaluate_ComplexRule_TypedContext | 143.551 ns | 0.5227 ns | 0.4889 ns | 144.228 ns | 142.255 ns | 144.083 ns |         - |
| Evaluate_ComplexRule_AOT          |  92.751 ns | 0.3692 ns | 0.3454 ns |  93.186 ns |  92.211 ns |  93.147 ns |         - |
| Evaluate_ComplexRule_Dictionary   | 183.088 ns | 0.5103 ns | 0.4524 ns | 183.825 ns | 182.072 ns | 183.677 ns |         - |
| Evaluate_10Rules_AOT              | 120.205 ns | 0.3945 ns | 0.3690 ns | 120.752 ns | 119.240 ns | 120.572 ns |         - |
| Evaluate_NoRules_AOT              |   6.731 ns | 0.0211 ns | 0.0187 ns |   6.761 ns |   6.691 ns |   6.758 ns |         - |
| Evaluate_50_50_Rollout_AOT        |  36.954 ns | 0.2066 ns | 0.1933 ns |  37.193 ns |  36.654 ns |  37.183 ns |         - |
| GetJsonVariation                  |  22.261 ns | 0.0649 ns | 0.0608 ns |  22.342 ns |  22.109 ns |  22.336 ns |         - |
| GetJsonVariation_WithUser_AOT     |  27.702 ns | 0.0656 ns | 0.0581 ns |  27.796 ns |  27.584 ns |  27.777 ns |         - |
| GetStringVariation                |  21.205 ns | 0.0581 ns | 0.0544 ns |  21.308 ns |  21.128 ns |  21.281 ns |         - |
| GetStringVariation_WithUser_AOT   |  27.152 ns | 0.0894 ns | 0.0836 ns |  27.266 ns |  26.977 ns |  27.265 ns |         - |
| Analytics_TrackEvent_Simple       |  44.589 ns | 0.1252 ns | 0.1171 ns |  44.766 ns |  44.354 ns |  44.764 ns |         - |
| Analytics_TrackEvent_10Rules_AOT  |  45.522 ns | 0.1571 ns | 0.1469 ns |  45.769 ns |  45.311 ns |  45.768 ns |         - |
