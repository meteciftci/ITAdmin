export type PagedItems<T> = {
  items: T[];
  totalPages: number;
};

export async function collectAllPagedItems<T>(
  pageSize: number,
  loadPage: (pageNumber: number, pageSize: number) => Promise<PagedItems<T>>,
): Promise<T[]> {
  const firstPage = await loadPage(1, pageSize);
  const items = [...firstPage.items];

  for (let pageNumber = 2; pageNumber <= firstPage.totalPages; pageNumber += 1) {
    const page = await loadPage(pageNumber, pageSize);
    items.push(...page.items);
  }

  return items;
}
