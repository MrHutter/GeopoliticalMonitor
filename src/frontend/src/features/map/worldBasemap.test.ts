import {
  countryNamesAtCoordinate,
  crimeaRepresentativeCoordinate,
  naturalEarthCountries,
} from './worldBasemap';

describe('worldBasemap sovereignty normalization', () => {
  it('assigns a representative Crimean interior coordinate only to Ukraine', () => {
    expect(countryNamesAtCoordinate(naturalEarthCountries, crimeaRepresentativeCoordinate)).toEqual(['Ukraine']);
  });

  it('retains representative mainland ownership and the expected country structure', () => {
    expect(countryNamesAtCoordinate(naturalEarthCountries, [30.52, 50.45])).toEqual(['Ukraine']);
    expect(countryNamesAtCoordinate(naturalEarthCountries, [37.62, 55.75])).toEqual(['Russia']);
    expect(naturalEarthCountries.features).toHaveLength(177);

    const ukraine = naturalEarthCountries.features.find((country) => String(country.id) === '804');
    const russia = naturalEarthCountries.features.find((country) => String(country.id) === '643');
    expect(ukraine?.geometry.type).toBe('MultiPolygon');
    expect(russia?.geometry.type).toBe('MultiPolygon');
    if (ukraine?.geometry.type !== 'MultiPolygon') throw new Error('Expected corrected Ukraine multipolygon.');
    expect(ukraine.geometry.coordinates).toHaveLength(1);
  });
});
