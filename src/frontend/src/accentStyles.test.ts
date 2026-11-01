import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Tests run from src/frontend; the stylesheet is read as text because jsdom does not resolve CSS variables.
const stylesheet = readFileSync(resolve(process.cwd(), 'src', 'styles.css'), 'utf8');
const accents = ['orange', 'blue', 'red', 'green', 'yellow'] as const;
const accentFollowingSelectors = ['.firms-window-label', '.firms-refresh-control button', '.firms-refresh-status.is-success'];
// Popup chrome follows the accent; the popup surface and its detail text stay neutral, so only these declarations are checked.
const accentFollowingPopupChrome = [
  ['.firms-map-popup .maplibregl-popup-content', 'border'],
  ['.firms-map-popup strong', 'color'],
] as const;
const hardCodedColor = /#[0-9a-f]{3,8}\b|rgba?\(|hsla?\(/i;
const accentVariable = /var\((--accent(?:-strong|-glow)?)\)/;

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

function declarationValue(body: string, property: string) {
  const value = body.match(new RegExp(`(?:^|[;\\s])${property}:\\s*([^;]+);`))?.[1].trim();
  if (!value) throw new Error(`Missing ${property} declaration`);
  return value;
}

function popupChromeVariables() {
  return accentFollowingPopupChrome
    .map(([selector, property]) => declarationValue(ruleBody(selector), property).match(accentVariable)?.[1])
    .filter((name): name is string => Boolean(name));
}

describe('accent-following FIRMS controls', () => {
  it.each(accentFollowingSelectors)('%s uses accent variables and no fixed color', (selector) => {
    const body = ruleBody(selector);
    expect(body).toMatch(accentVariable);
    expect(body).not.toMatch(hardCodedColor);
  });

  it.each(accentFollowingPopupChrome)('the FIRMS popup %s %s uses an accent variable and no fixed color', (selector, property) => {
    const value = declarationValue(ruleBody(selector), property);
    expect(value).toMatch(accentVariable);
    expect(value).not.toMatch(hardCodedColor);
  });

  it.each(accents)('the %s accent defines its own values for the variables those controls and the popup chrome use', (accent) => {
    for (const name of new Set(['--accent-strong', '--accent-glow', ...popupChromeVariables()])) {
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
