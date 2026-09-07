// Публикует C#-помощники (FloVMP.Launcher.Native + FloVMP.Connect) как
// self-contained single-file win-x64 в electron/native-dist/, откуда
// electron-builder кладёт их в resources/native/. Так конечному игроку
// НЕ нужен установленный .NET 8.
//
// Запуск: node build-native.mjs   (вызывается из npm run dist)
import { execSync } from 'node:child_process';
import { rmSync, mkdirSync, cpSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const out = path.join(here, 'native-dist');

const projects = [
  path.join(repo, 'launcher', 'src', 'FloVMP.Launcher.Native', 'FloVMP.Launcher.Native.csproj'),
  path.join(repo, 'launcher', 'src', 'FloVMP.Connect', 'FloVMP.Connect.csproj'),
];

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

for (const proj of projects) {
  if (!existsSync(proj)) throw new Error('нет проекта: ' + proj);
  const name = path.basename(proj, '.csproj');
  const stage = path.join(here, '.native-stage', name);
  console.log(`\n>>> publish ${name} (self-contained single-file win-x64)`);
  execSync(
    `dotnet publish "${proj}" -c Release -r win-x64 --self-contained true ` +
      `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ` +
      `-p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false ` +
      `-o "${stage}"`,
    { stdio: 'inherit', cwd: repo },
  );
  cpSync(stage, out, { recursive: true });
}

console.log('\nnative-dist готова:', out);
