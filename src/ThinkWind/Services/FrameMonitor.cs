// Adapted from the user-owned Think Monitor source; runs independently in Think Wind.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ThinkWind.Services;

public sealed class FrameMonitor : IDisposable
{
	private static readonly string SessionName = "ThinkWind-Frames-" + Environment.ProcessId;

	private static readonly Guid DxgiProvider = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

	private static readonly Guid D3D9Provider = new Guid("783ACA0A-790E-4D7F-8451-AA850511C6B9");

	private const int DxgiPresentStart = 42;

	private const int D3D9PresentStart = 1;

	private const double HistoryMs = 10000.0;

	private TraceEventSession? _session;

	private Thread? _thread;

	private readonly object _lifecycle = new object();

	private bool _disposed;

	private readonly object _lock = new object();

	private readonly Dictionary<int, Queue<double>> _presents = new Dictionary<int, Queue<double>>();

	private readonly Dictionary<int, long> _lastSeen = new Dictionary<int, long>();

	public bool Running => _thread?.IsAlive ?? false;

	public string? Error { get; private set; }

	public void Start()
	{
		lock (_lifecycle)
		{
			if (_disposed || Running)
			{
				return;
			}
			TraceEventSession? session = null;
			try
			{
				session = new TraceEventSession(SessionName)
				{
					StopOnDispose = true
				};
				session.EnableProvider(DxgiProvider, TraceEventLevel.Informational, ulong.MaxValue);
				session.EnableProvider(D3D9Provider, TraceEventLevel.Informational, ulong.MaxValue);
				session.Source.AllEvents += OnEvent;
				_session = session;
				_thread = new Thread(() =>
				{
					try
					{
						session.Source.Process();
					}
					catch (Exception ex2)
					{
						Error = ex2.Message;
					}
				})
				{
					IsBackground = true,
					Name = "ETW frames"
				};
				Error = null;
				_thread.Start();
			}
			catch (Exception ex)
			{
				Error = ex.Message;
				try
				{
					session?.Dispose();
				}
				catch
				{
				}
				_session = null;
				_thread = null;
			}
		}
	}

	private void OnEvent(TraceEvent e)
	{
		int iD = (int)e.ID;
		if ((!(e.ProviderGuid == DxgiProvider) || iD != 42) && (!(e.ProviderGuid == D3D9Provider) || iD != 1))
		{
			return;
		}
		GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground); if (e.ProcessID != foreground || foreground == Environment.ProcessId) return;
        double timeStampRelativeMSec = e.TimeStampRelativeMSec;
		lock (_lock)
		{
			if (!_presents.TryGetValue(e.ProcessID, out var value))
			{
				value = (_presents[e.ProcessID] = new Queue<double>());
			}
			value.Enqueue(timeStampRelativeMSec);
            while (value.Count > 4096) value.Dequeue();
            foreach (int stale in _presents.Keys.Where(pid => pid != e.ProcessID).ToArray()) { _presents.Remove(stale); _lastSeen.Remove(stale); }
			while (value.Count > 0 && value.Peek() < timeStampRelativeMSec - 10000.0)
			{
				value.Dequeue();
			}
			_lastSeen[e.ProcessID] = Stopwatch.GetTimestamp();
		}
	}

	public FrameStats? GetStats(int pid)
	{
		double[] array;
		lock (_lock)
		{
			if (!_presents.TryGetValue(pid, out var value) || value.Count < 3)
			{
				return null;
			}
			if (Stopwatch.GetElapsedTime(_lastSeen[pid]).TotalSeconds > 2.0)
			{
				return null;
			}
			array = value.ToArray();
		}
		double last = array[^1];
		List<double> list = new List<double>(array.Length);
		for (int i = 1; i < array.Length; i++)
		{
			list.Add(array[i] - array[i - 1]);
		}
		int num = Array.FindIndex(array, (double x) => x >= last - 1000.0);
		double num2 = last - array[num];
		int num3 = array.Length - 1 - num;
		double num4 = ((num2 > 0.0) ? ((double)(num3 * 1000) / num2) : 0.0);
		double[] array2 = list.OrderBy((double x) => x).ToArray();
		double num5 = array2[(int)Math.Min(array2.Length - 1, Math.Floor((double)array2.Length * 0.99))];
		double low1Fps = ((num5 > 0.0) ? (1000.0 / num5) : 0.0);
		List<double> recentFrameTimes = list.Skip(Math.Max(0, list.Count - 120)).ToList();
		return new FrameStats(pid, ProcessName(pid), num4, (num4 > 0.0) ? (1000.0 / num4) : 0.0, low1Fps, recentFrameTimes);
	}

	public FrameStats? GetForegroundStats()
	{
		nint foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return null;
		}
		GetWindowThreadProcessId(foregroundWindow, out var pid);
		if (pid != 0 && pid != Environment.ProcessId)
		{
			return GetStats((int)pid);
		}
		return null;
	}

	private static string ProcessName(int pid)
	{
		try
		{
			using Process process = Process.GetProcessById(pid);
			return process.ProcessName;
		}
		catch
		{
			return $"PID {pid}";
		}
	}

	public void Stop()
	{
		lock (_lifecycle)
		{
			try
			{
				_session?.Source.StopProcessing();
			}
			catch
			{
			}
			try
			{
				_session?.Dispose();
			}
			catch
			{
			}
			_session = null;
			_thread = null;
			lock (_lock)
			{
				_presents.Clear();
				_lastSeen.Clear();
			}
		}
	}

	public void Dispose()
	{
		lock (_lifecycle)
		{
			_disposed = true;
			Stop();
		}
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);
}
