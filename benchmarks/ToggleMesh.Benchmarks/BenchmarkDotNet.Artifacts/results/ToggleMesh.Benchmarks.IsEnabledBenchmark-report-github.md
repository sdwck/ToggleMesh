```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  

```
| Method                           | Mean       | Error     | StdDev    | Max        | Min        | P95        | Allocated |
|----------------------------------|-----------:|----------:|----------:|-----------:|-----------:|-----------:|----------:|
| Evaluate_1Rule_TypedContext      |  67.467 ns | 0.7700 ns | 0.7203 ns |  68.638 ns |  66.245 ns |  68.569 ns |         - |
| Evaluate_1Rule_AOT               |  29.841 ns | 0.6063 ns | 0.7217 ns |  30.733 ns |  28.782 ns |  30.648 ns |         - |
| Evaluate_1Rule_Dictionary        | 112.657 ns | 2.2260 ns | 2.3818 ns | 117.369 ns | 110.319 ns | 117.284 ns |         - |
| Evaluate_ComplexRule_TypedContext | 144.747 ns | 0.1534 ns | 0.1360 ns | 145.015 ns | 144.576 ns | 144.965 ns |         - |
| Evaluate_ComplexRule_AOT         |  94.681 ns | 0.1634 ns | 0.1528 ns |  94.940 ns |  94.416 ns |  94.873 ns |         - |
| Evaluate_ComplexRule_Dictionary  | 200.159 ns | 3.9238 ns | 3.8537 ns | 209.292 ns | 196.543 ns | 206.678 ns |         - |
| Evaluate_10Rules_AOT             | 117.107 ns | 1.2149 ns | 1.0770 ns | 118.850 ns | 115.391 ns | 118.705 ns |         - |
| Evaluate_NoRules_AOT             |   7.281 ns | 0.0344 ns | 0.0321 ns |   7.342 ns |   7.222 ns |   7.329 ns |         - |
| Evaluate_50_50_Rollout_AOT       |  38.536 ns | 0.3945 ns | 0.3691 ns |  39.045 ns |  37.792 ns |  38.980 ns |         - |
| GetJsonVariation                 |  25.509 ns | 0.0208 ns | 0.0184 ns |  25.547 ns |  25.481 ns |  25.535 ns |         - |
| GetJsonVariation_WithUser_AOT    |  27.864 ns | 0.0445 ns | 0.0394 ns |  27.947 ns |  27.813 ns |  27.935 ns |         - |
| GetStringVariation               |  22.827 ns | 0.1537 ns | 0.1437 ns |  23.072 ns |  22.615 ns |  23.069 ns |         - |
| GetStringVariation_WithUser_AOT  |  28.788 ns | 0.2499 ns | 0.2337 ns |  29.138 ns |  28.330 ns |  29.098 ns |         - |
| Analytics_TrackEvent_Simple      |  45.324 ns | 0.3558 ns | 0.3329 ns |  45.911 ns |  44.979 ns |  45.818 ns |         - |
| Analytics_TrackEvent_10Rules_AOT |  46.434 ns | 0.1356 ns | 0.1268 ns |  46.657 ns |  46.246 ns |  46.631 ns |         - |
