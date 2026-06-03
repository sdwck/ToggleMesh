```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host] : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  

```
| Method              | Mean     | Error   | StdDev  | Gen0   | Allocated |
|-------------------- |---------:|--------:|--------:|-------:|----------:|
| IsEnabled_WithRules | 182.2 ns | 1.54 ns | 1.44 ns | 0.0694 |   1.17 KB |
