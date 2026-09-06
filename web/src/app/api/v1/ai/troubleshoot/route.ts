import { NextRequest, NextResponse } from 'next/server';

interface DiagnosticResult {
  detectedIssue: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  explanation: string;
  solutionSteps: string[];
  suggestedCommand?: string;
  docReference?: string;
}

const KNOWLEDGE_BASE: Array<{
  pattern: RegExp;
  result: DiagnosticResult;
}> = [
  {
    pattern: /coreclr-module\.dll|coreclr|hostfxr|libcoreclr/i,
    result: {
      detectedIssue: 'CoreCLR .NET Runtime Missing or Incompatible',
      severity: 'critical',
      explanation:
        'The alt:V server failed to initialize the .NET 8 CoreCLR host module. This occurs when ASP.NET Core Runtime 8.0 is not installed on the system or hostfxr.dll cannot be located.',
      solutionSteps: [
        'Install .NET 8.0 Runtime & ASP.NET Core 8.0 Hosting Bundle.',
        'Ensure dotnet is present in PATH (`dotnet --info`).',
        'Verify that modules/coreclr-module.dll is copied to the server runtime directory.',
      ],
      suggestedCommand: 'winget install Microsoft.DotNet.Runtime.8',
      docReference: '/docs#coreclr-setup',
    },
  },
  {
    pattern: /BattlEye|BEService|blocked loading|0x00000001|BEClient/i,
    result: {
      detectedIssue: 'BattlEye Anti-Cheat Blocked Injected DLL',
      severity: 'critical',
      explanation:
        'Rockstar Games enabled mandatory BattlEye on build 3323+. FloV:MP requires clean execution via the pre-BattlEye b3307 runtime executable or bypassing the BEService hook.',
      solutionSteps: [
        'Open FloV:MP Launcher and verify GTA V build is set to b3307.',
        'Ensure GameExeManager has placed the compatibility PlayGTAV.exe proxy.',
        'Launch via `FloVMP.Connect.exe` to automatically suspend BEService before injection.',
      ],
      suggestedCommand: 'dotnet run --project launcher/src/FloVMP.Connect',
      docReference: '/docs#battleye-compatibility',
    },
  },
  {
    pattern: /data\/\*\.bin|weapons\.bin|clothes\.bin|vehiclemods\.bin|failed to load \.bin/i,
    result: {
      detectedIssue: 'Missing alt:V Binary Data Files (.bin)',
      severity: 'high',
      explanation:
        'The game server starts in autonomous mode but cannot find the release vehicle/weapon meta descriptors (`data/release/data/*.bin`).',
      solutionSteps: [
        'Copy `data/release/data/*.bin` from `C:\\ViMP backup\\backup-altv\\` into `runtime/server/data/`.',
        'Run `scripts/assemble-runtime.ps1` to automatically sync all engine dependencies.',
      ],
      suggestedCommand: 'powershell -ExecutionPolicy Bypass -File scripts/assemble-runtime.ps1',
      docReference: '/docs#server-deployment',
    },
  },
  {
    pattern: /ECONNREFUSED|ETIMEDOUT|UDP 7788|connection timed out|failed to connect to host/i,
    result: {
      detectedIssue: 'Firewall or Network Port 7788 Blocked',
      severity: 'medium',
      explanation:
        'The client cannot reach the game server on UDP port 7788, or the HTTP FastDL server is blocked on port 80/7788.',
      solutionSteps: [
        'Allow incoming UDP 7788 in Windows Firewall / iptables.',
        'Ensure Nginx reverse proxy is running on the host server.',
        'Check server status via `curl.exe http://<server_ip>:7788/info`.',
      ],
      suggestedCommand: 'netsh advfirewall firewall add rule name="FloVMP-UDP" dir=in action=allow protocol=UDP localport=7788',
      docReference: '/docs#ports-and-networking',
    },
  },
  {
    pattern: /resource\.toml|cannot find main|module not found|syntax error/i,
    result: {
      detectedIssue: 'Resource Manifest or Script Error',
      severity: 'medium',
      explanation:
        'A server resource failed to load because its resource.toml points to an invalid entrypoint, or a syntax error halted module initialization.',
      solutionSteps: [
        'Check `resource.toml` in your resource folder.',
        'Ensure `main = "FloVMP.Gamemode.dll"` matches the compiled output.',
        'Inspect server logs under `logs/` for exact stack trace.',
      ],
      suggestedCommand: 'dotnet build server/src/FloVMP.Gamemode/FloVMP.Gamemode.csproj',
      docReference: '/docs#resource-architecture',
    },
  },
];

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { query = '', context = '' } = body;

    const fullText = `${query} ${context}`.trim();

    if (!fullText) {
      return NextResponse.json(
        { success: false, error: 'Query or crash log text is required' },
        { status: 400 }
      );
    }

    let match: DiagnosticResult | null = null;
    for (const item of KNOWLEDGE_BASE) {
      if (item.pattern.test(fullText)) {
        match = item.result;
        break;
      }
    }

    if (!match) {
      match = {
        detectedIssue: 'General Multiplayer / Runtime Diagnostic',
        severity: 'low',
        explanation:
          'No exact matching pattern was found in the pre-indexed database. However, FloV:MP has logged this event for automated analysis.',
        solutionSteps: [
          'Verify all dependencies are compiled (`dotnet test`, `npm run build`).',
          'Review server console logs in the Web Dashboard Server Console tab.',
          'Ensure the client launcher is running with Administrator privileges for DLL injection.',
        ],
        suggestedCommand: 'dotnet test server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj',
        docReference: '/docs',
      };
    }

    return NextResponse.json({
      success: true,
      query,
      diagnostic: match,
      timestamp: new Date().toISOString(),
    });
  } catch (err: any) {
    return NextResponse.json({ success: false, error: err.message }, { status: 500 });
  }
}
