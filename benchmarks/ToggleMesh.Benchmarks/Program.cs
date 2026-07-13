using BenchmarkDotNet.Running;
using ToggleMesh.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
