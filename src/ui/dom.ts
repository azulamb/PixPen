/** Minimal framework-free DOM construction helper. */

type Children = (Node | string)[];

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  props?: Record<string, unknown> & { className?: string; text?: string },
  children?: Children,
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (props) {
    for (const [key, value] of Object.entries(props)) {
      if (key === "className") {
        node.className = value as string;
      } else if (key === "text") {
        node.textContent = value as string;
      } else if (key.startsWith("on") && typeof value === "function") {
        node.addEventListener(
          key.slice(2).toLowerCase(),
          value as EventListener,
        );
      } else if (value !== undefined && value !== null) {
        (node as unknown as Record<string, unknown>)[key] = value;
      }
    }
  }
  if (children) {
    for (const child of children) {
      node.append(child);
    }
  }
  return node;
}

export function clear(node: Element) {
  node.replaceChildren();
}
