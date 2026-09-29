import { applyProductDocumentTitle, productDisplayName, productDisplayNameUpper } from './productIdentity';

describe('product identity', () => {
  it('provides the current provisional display name to frontend consumers', () => {
    expect(productDisplayName).toBe('Geopolitical Monitor');
    expect(productDisplayNameUpper).toBe('GEOPOLITICAL MONITOR');
  });

  it('applies the centralized display name to the browser document title', () => {
    const target = { title: '' };

    applyProductDocumentTitle(target);

    expect(target.title).toBe(productDisplayName);
  });
});
