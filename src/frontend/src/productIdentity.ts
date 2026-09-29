export const productDisplayName = 'Geopolitical Monitor';
export const productDisplayNameUpper = productDisplayName.toUpperCase();

export function applyProductDocumentTitle(target: Pick<Document, 'title'> = document) {
  target.title = productDisplayName;
}
