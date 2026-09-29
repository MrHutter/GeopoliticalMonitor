import type { StyleSpecification } from 'maplibre-gl';
import { productDisplayName } from '../../productIdentity';
import { naturalEarthCountries } from './worldBasemap';

export const countrySourceId = 'natural-earth-countries-110m';
export const countryFillLayerId = 'natural-earth-country-fill';
export const countryBoundaryLayerId = 'natural-earth-country-boundaries';

export const mapStyleSources = {
  countries: naturalEarthCountries,
};

export function createLocalMapStyle(): StyleSpecification {
  return {
    version: 8,
    name: `${productDisplayName} local dark basemap`,
    sources: {
      [countrySourceId]: {
        type: 'geojson',
        data: mapStyleSources.countries,
      },
    },
    layers: [
      {
        id: 'ocean-background',
        type: 'background',
        paint: {
          'background-color': '#000000',
        },
      },
      {
        id: countryFillLayerId,
        type: 'fill',
        source: countrySourceId,
        paint: {
          'fill-color': '#191a1c',
          'fill-opacity': 0.94,
        },
      },
      {
        id: countryBoundaryLayerId,
        type: 'line',
        source: countrySourceId,
        paint: {
          'line-color': '#686d75',
          'line-opacity': 0.58,
          'line-width': ['interpolate', ['linear'], ['zoom'], 0, 0.45, 4, 0.9, 6, 1.2],
        },
      },
    ],
  };
}
