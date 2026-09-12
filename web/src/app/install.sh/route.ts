import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

/**
 * GET /install.sh
 * Отдаёт официальный bash-скрипт авто-установки FloV:MP в 1 команду.
 */
export async function GET(req: NextRequest) {
  try {
    const scriptPath = path.join(process.cwd(), '..', 'scripts', 'install.sh');
    let content = '';

    if (fs.existsSync(scriptPath)) {
      content = fs.readFileSync(scriptPath, 'utf8');
    } else {
      // Fallback внутри web/public или относительного пути
      const localPath = path.join(process.cwd(), 'public', 'install.sh');
      if (fs.existsSync(localPath)) {
        content = fs.readFileSync(localPath, 'utf8');
      } else {
        return new NextResponse('#!/bin/bash\necho "FloV:MP installer not found"\nexit 1', {
          status: 404,
          headers: { 'Content-Type': 'text/x-shellscript; charset=utf-8' },
        });
      }
    }

    return new NextResponse(content, {
      status: 200,
      headers: {
        'Content-Type': 'text/x-shellscript; charset=utf-8',
        'Cache-Control': 'no-cache, no-store, must-revalidate',
      },
    });
  } catch (err: any) {
    return new NextResponse(`#!/bin/bash\necho "Error loading installer: ${err.message}"\nexit 1`, {
      status: 500,
      headers: { 'Content-Type': 'text/x-shellscript; charset=utf-8' },
    });
  }
}
