export function normalizeUserId(userId: string | null | undefined): string | null {
  const trimmed = userId?.trim();
  return trimmed ? trimmed.toLowerCase() : null;
}

export function isSelfRoleTarget(
  actorUserId: string | null | undefined,
  targetUserId: string | null | undefined,
): boolean {
  const actor = normalizeUserId(actorUserId);
  const target = normalizeUserId(targetUserId);
  return Boolean(actor && target && actor === target);
}
