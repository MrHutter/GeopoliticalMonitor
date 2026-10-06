import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

// Tests run from src/frontend, so the repository root is two levels up.
const repositoryRoot = resolve(process.cwd(), '..', '..');
const guideDirectory = join(repositoryRoot, 'docs', 'guide');
const userFacingDocuments = [
  join(repositoryRoot, 'README.md'),
  join(repositoryRoot, 'docs', 'README.md'),
  ...readdirSync(guideDirectory).filter((name) => name.endsWith('.md')).map((name) => join(guideDirectory, name)),
];

const sourceRoots = [join(repositoryRoot, 'src', 'backend'), join(repositoryRoot, 'src', 'frontend', 'src')];
const skippedDirectories = new Set(['bin', 'obj', 'node_modules', 'dist']);

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return skippedDirectories.has(entry.name) ? [] : sourceFiles(path);
    return /\.(cs|ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

function relativeLinkTargets(markdown: string) {
  return [...markdown.matchAll(/\]\(([^)\s]+)\)/g)]
    .map((match) => match[1])
    .filter((target) => !/^[a-z]+:/i.test(target) && !target.startsWith('#'))
    .map((target) => decodeURIComponent(target.split('#')[0]));
}

describe('user-facing documentation', () => {
  it('includes the guides', () => {
    expect(userFacingDocuments.length).toBeGreaterThanOrEqual(5);
  });

  it.each(userFacingDocuments.map((path) => [path.slice(repositoryRoot.length + 1), path]))('%s links only to existing files', (_name, path) => {
    const missing = relativeLinkTargets(readFileSync(path, 'utf8')).filter((target) => !existsSync(resolve(dirname(path), target)));
    expect(missing).toEqual([]);
  });

  it('names only environment variables that the source reads', () => {
    const named = new Set(userFacingDocuments.flatMap((path) => readFileSync(path, 'utf8').match(/GEOPOLITICALMONITOR_[A-Z0-9_]+/g) ?? []));
    const source = sourceRoots.flatMap(sourceFiles).filter((path) => !path.endsWith('docs.test.ts')).map((path) => readFileSync(path, 'utf8')).join('\n');
    expect([...named].filter((variable) => !source.includes(variable))).toEqual([]);
  });

  it.each(userFacingDocuments.map((path) => [path.slice(repositoryRoot.length + 1), path]))('%s contains no profile path or banned word', (_name, path) => {
    const text = readFileSync(path, 'utf8');
    expect(text.toLowerCase()).not.toContain('c:\\users\\');
    expect(text.toLowerCase()).not.toContain('hybrid');
  });
});
