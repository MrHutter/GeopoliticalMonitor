import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Tests run from src/frontend; the stylesheet is read as text because jsdom does not resolve CSS variables.
const stylesheet = readFileSync(resolve(process.cwd(), 'src', 'styles.css'), 'utf8');
const accents = ['orange', 'blue', 'red', 'green', 'yellow'] as const;
const accentFollowingSelectors = ['.firms-window-label', '.firms-refresh-control button', '.firms-refresh-status.is-success'];
const hardCodedColor = /#[0-9a-f]{3,8}\b|rgba?\(|hsla?\(/i;

function ruleBody(selector: string) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = stylesheet.match(new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`));
  if (!match) throw new Error(`Missing rule ${selector}`);
  return match[1];
}

function accentBlock(accent: (typeof accents)[number]) {
  return accent === 'orange' ? ruleBody(':root') : ruleBody(`:root[data-accent='${accent}']`);
}

function variableValue(block: string, name: string) {
  return block.match(new RegExp(`${name}:\\s*([^;]+);`))?.[1].trim();
}

describe('accent-following FIRMS controls', () => {
  it.each(accentFollowingSelectors)('%s uses accent variables and no fixed color', (selector) => {
    const body = ruleBody(selector);
    expect(body).toMatch(/var\(--accent(?:-strong|-glow)?\)/);
    expect(body).not.toMatch(hardCodedColor);
  });

  it.each(accents)('the %s accent defines its own values for the variables those controls use', (accent) => {
    for (const name of ['--accent-strong', '--accent-glow']) {
      const value = variableValue(accentBlock(accent), name);
      expect(value).toBeTruthy();
      if (accent !== 'orange') {
        expect(value).not.toBe(variableValue(accentBlock('orange'), name));
      }
    }
  });

  it('keeps the FIRMS age colors independent of the accent', () => {
    for (const bucket of ['under-3h', '3-6h', '6-12h', '12-24h']) {
      expect(stylesheet).toMatch(new RegExp(`\\.firms-age-${bucket} \\{ background: #[0-9a-f]{6}; \\}`));
    }
  });
});
