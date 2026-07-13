```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  

```
| Method                            | Mean       | Error     | StdDev    | Max        | Min        | P95        | Allocated |
|---------------------------------- |-----------:|----------:|----------:|-----------:|-----------:|-----------:|----------:|
| Evaluate_1Rule_TypedContext       |  70.160 ns | 0.4558 ns | 0.4264 ns |  71.189 ns |  69.629 ns |  70.772 ns |         - |
| Evaluate_1Rule_AOT                |  29.533 ns | 0.1249 ns | 0.1107 ns |  29.667 ns |  29.269 ns |  29.658 ns |         - |
| Evaluate_1Rule_Dictionary         |  79.205 ns | 0.3959 ns | 0.3703 ns |  79.800 ns |  78.534 ns |  79.686 ns |         - |
| Evaluate_ComplexRule_TypedContext | 145.408 ns | 0.6078 ns | 0.5686 ns | 146.411 ns | 144.294 ns | 146.129 ns |         - |
| Evaluate_ComplexRule_AOT          |  96.656 ns | 0.5116 ns | 0.4785 ns |  97.686 ns |  95.977 ns |  97.468 ns |         - |
| Evaluate_ComplexRule_Dictionary   | 160.223 ns | 0.3670 ns | 0.3254 ns | 160.718 ns | 159.554 ns | 160.640 ns |         - |
| Evaluate_10Rules_AOT              | 114.230 ns | 0.4029 ns | 0.3769 ns | 114.939 ns | 113.569 ns | 114.758 ns |         - |
| Evaluate_NoRules_AOT              |   7.432 ns | 0.0452 ns | 0.0401 ns |   7.495 ns |   7.358 ns |   7.484 ns |         - |
| Evaluate_50_50_Rollout_AOT        |  38.266 ns | 0.1731 ns | 0.1620 ns |  38.558 ns |  38.038 ns |  38.517 ns |         - |
| GetJsonVariation                  |  28.685 ns | 0.1165 ns | 0.1090 ns |  28.830 ns |  28.449 ns |  28.798 ns |         - |
| GetStringVariation                |  28.440 ns | 0.0130 ns | 0.0122 ns |  28.461 ns |  28.420 ns |  28.459 ns |         - |
| Analytics_TrackEvent_Simple       |  43.758 ns | 0.0699 ns | 0.0584 ns |  43.920 ns |  43.715 ns |  43.860 ns |         - |
| Analytics_TrackEvent_10Rules_AOT  |  45.841 ns | 0.0764 ns | 0.0597 ns |  45.978 ns |  45.774 ns |  45.931 ns |         - |
