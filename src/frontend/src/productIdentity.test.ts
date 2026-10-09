import { applyProductDocumentTitle, productDisplayName, productDisplayNameUpper } from './productIdentity';

describe('product identity', () => {
  it('provides the decided display name to frontend consumers', () => {
    expect(productDisplayName).toBe('Northern Monitor');
    expect(productDisplayNameUpper).toBe('NORTHERN MONITOR');
  });

  it('applies the centralized display name to the browser document title', () => {
    const target = { title: '' };

    applyProductDocumentTitle(target);

    expect(target.title).toBe(productDisplayName);
  });
});
