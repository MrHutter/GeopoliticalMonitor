import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { getCategoryColor } from './features/map/mapEvents';

const css = readFileSync(join(process.cwd(), 'src', 'styles.css'), 'utf8');

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function declarationsFor(selector: string) {
  const match = css.match(new RegExp(`${escapeRegExp(selector)}\\s*\\{([\\s\\S]*?)\\}`));
  if (!match) throw new Error(`Missing CSS selector: ${selector}`);
  return match[1];
}

function customPropertiesFor(selector: string) {
  const declarations = declarationsFor(selector);
  const properties: Record<string, string> = {};
  for (const match of declarations.matchAll(/(--[a-z0-9-]+):\s*([^;]+);/gi)) {
    properties[match[1]] = match[2].trim();
  }
  return properties;
}

describe('dark palette tokens', () => {
  it('keeps all accent variants on one shared neutral dark base', () => {
    const root = customPropertiesFor(':root');

    expect(root).toMatchObject({
      '--bg': '#000000',
      '--surface': '#040404',
      '--surface-strong': '#090909',
      '--surface-soft': '#020202',
      '--line': '#1b1b1d',
      '--line-strong': '#2a2a2d',
      '--text': '#d4d6da',
    });

    const baseTokens = ['--bg', '--surface', '--surface-strong', '--surface-soft', '--line', '--line-strong', '--text', '--muted', '--muted-strong'];
    for (const accent of ['blue', 'red', 'green', 'yellow']) {
      const properties = customPropertiesFor(`:root[data-accent='${accent}']`);
      for (const baseToken of baseTokens) {
        expect(properties).not.toHaveProperty(baseToken);
      }
    }
  });

  it('allows accent variants to override accent and glow tokens only', () => {
    const allowedAccentTokens = new Set(['--accent', '--accent-strong', '--accent-soft', '--focus', '--accent-glow', '--accent-glow-strong']);

    for (const accent of ['blue', 'red', 'green', 'yellow']) {
      const properties = customPropertiesFor(`:root[data-accent='${accent}']`);
      expect(Object.keys(properties).sort()).toEqual([...allowedAccentTokens].sort());
    }
  });

  it('keeps semantic event colors independent from accent themes', () => {
    const root = customPropertiesFor(':root');
    for (const semanticToken of ['--critical', '--security', '--civil', '--infrastructure', '--humanitarian', '--maritime']) {
      expect(root[semanticToken]).toBeTruthy();
    }

    expect(getCategoryColor('MilitaryConflict')).toBe('#d95d62');
    expect(getCategoryColor('Security')).toBe('#6da3d9');
    expect(getCategoryColor('CivilUnrest')).toBe('#9f76d1');
    expect(getCategoryColor('Infrastructure')).toBe('#55b5a8');
    expect(getCategoryColor('Humanitarian')).toBe('#d9858e');
  });

  it('keeps the severity scale fixed across accent themes and distinct for catastrophic', () => {
    const root = customPropertiesFor(':root');
    const severityTokens = ['--low', '--medium', '--high', '--critical', '--catastrophic'];

    expect(root).toMatchObject({
      '--low': '#78a781',
      '--medium': '#d3b85f',
      '--high': '#d67b3d',
      '--critical': '#e05a5a',
      '--catastrophic': '#a52a6b',
    });
    for (const accent of ['blue', 'red', 'green', 'yellow']) {
      const properties = customPropertiesFor(`:root[data-accent='${accent}']`);
      for (const token of severityTokens) {
        expect(properties).not.toHaveProperty(token);
      }
    }

    const catastrophic = root['--catastrophic'].toLowerCase();
    const reserved = [root['--critical'], '#ff3b4f', getCategoryColor('MilitaryConflict'), '#ff9a3d', '#d77a36', '#9f6040', '#6f4c43']
      .map((color) => color.toLowerCase());
    expect(reserved).not.toContain(catastrophic);
  });

  it('keeps lifecycle status dots off the severity color tokens', () => {
    for (const selector of ['.status-dot', '.status-unverified,\n.event-row .status-unverified', '.status-active,\n.event-row .status-active', '.status-developing,\n.event-row .status-developing', '.status-resolved,\n.event-row .status-resolved']) {
      expect(declarationsFor(selector)).not.toMatch(/var\(--(low|medium|high|critical|catastrophic)\)/);
    }

    expect(declarationsFor('.severity-catastrophic')).toContain('var(--catastrophic)');
    expect(declarationsFor('.unverified-badge')).not.toMatch(/var\(--(critical|high)\)/);
  });

  it('styles provider status neutrally, with an attention tone that is not a severity or accent token', () => {
    const root = customPropertiesFor(':root');
    const attention = root['--status-attention'].toLowerCase();
    const severityAndAccent = ['--low', '--medium', '--high', '--critical', '--catastrophic', '--accent', '--accent-strong'].map((token) => root[token].toLowerCase());

    expect(attention).toBeTruthy();
    expect(severityAndAccent).not.toContain(attention);
    for (const accent of ['blue', 'red', 'green', 'yellow']) {
      expect(customPropertiesFor(`:root[data-accent='${accent}']`)).not.toHaveProperty('--status-attention');
    }

    const statusRules = css.match(/\.provider-status-[\s\S]*?\}/g) ?? [];
    expect(statusRules.length).toBeGreaterThan(4);
    for (const rule of statusRules) {
      expect(rule).not.toMatch(/var\(--(low|medium|high|critical|catastrophic|accent[a-z-]*)\)/);
      expect(rule).not.toMatch(/box-shadow|text-shadow|glow/);
    }
  });

  it('contains reversible workspace and modal Settings rules', () => {
    expect(css).toContain(".workspace-grid[data-x-pane-side='left']");
    expect(css).toContain(".workspace-grid[data-x-pane-side='right'] .event-row.is-selected");
    expect(declarationsFor('.settings-backdrop')).toContain('position: fixed;');
    expect(declarationsFor('.settings-backdrop')).toContain('inset: 0;');
    expect(declarationsFor('.settings-panel')).toContain('height: 100%;');
    expect(css).toContain('.settings-accent-options');
    expect(css).toContain('@media (max-width: 1120px)');
  });

  it('keeps events bounded and map flexible without a one-column workspace fallback', () => {
    const workspace = declarationsFor('.workspace-grid');
    const leftWorkspace = declarationsFor(".workspace-grid[data-x-pane-side='left']");

    expect(workspace).toContain('--events-panel-width: clamp(300px, 28%, 320px);');
    expect(workspace).toContain('grid-template-columns: var(--events-panel-width) minmax(0, 1fr);');
    expect(leftWorkspace).toContain('grid-template-columns: minmax(0, 1fr) var(--events-panel-width);');
    expect(css).not.toContain('grid-template-columns: minmax(0, 1fr);');
    expect(css).not.toContain('order: initial;');
  });
});
