export const productDisplayName = 'Northern Monitor';
export const productDisplayNameUpper = productDisplayName.toUpperCase();

export function applyProductDocumentTitle(target: Pick<Document, 'title'> = document) {
  target.title = productDisplayName;
}
