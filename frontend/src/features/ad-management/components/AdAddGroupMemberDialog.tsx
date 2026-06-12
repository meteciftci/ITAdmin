import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import {
  getAdGroupMemberPrimaryLabel,
  getAdGroupMemberSecondaryLabel,
} from "@/features/ad-management/ad-group-display-labels";
import { getAdGroupMemberTypeLabel } from "@/features/ad-management/ad-group-labels";
import {
  addAdGroupMember,
  AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY,
  invalidateAdGroupMemberQueries,
  searchAdGroupMemberCandidates,
} from "@/features/ad-management/api";
import { AdMembershipSelectionChips } from "@/features/ad-management/components/AdMembershipSelectionChips";
import {
  notifySequentialAddResults,
  partitionSequentialAddResults,
  runSequentialMembershipAdd,
} from "@/features/ad-management/run-sequential-membership-add";
import type {
  AdGroupMemberCandidateItem,
  AdGroupMemberCandidateType,
} from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

const MIN_SEARCH_LENGTH = 2;

type Props = {
  open: boolean;
  groupId: string;
  onOpenChange: (open: boolean) => void;
};

export function AdAddGroupMemberDialog({ open, groupId, onOpenChange }: Props) {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<"all" | AdGroupMemberCandidateType>("all");
  const [selectedCandidates, setSelectedCandidates] = useState<AdGroupMemberCandidateItem[]>([]);

  const candidateTypes = useMemo<AdGroupMemberCandidateType[] | undefined>(() => {
    if (typeFilter === "all") {
      return ["user", "group", "computer"];
    }
    return [typeFilter];
  }, [typeFilter]);

  const selectedDns = useMemo(
    () => new Set(selectedCandidates.map((candidate) => candidate.distinguishedName)),
    [selectedCandidates],
  );

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      setSearch("");
      setTypeFilter("all");
      setSelectedCandidates([]);
    }
    onOpenChange(nextOpen);
  };

  const candidatesQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY,
      groupId,
      "candidates",
      search,
      typeFilter,
    ],
    queryFn: () =>
      searchAdGroupMemberCandidates(groupId, {
        search,
        types: candidateTypes,
        pageSize: 50,
      }),
    enabled: open && search.trim().length >= MIN_SEARCH_LENGTH,
  });

  const addMutation = useMutation({
    mutationFn: async (candidates: AdGroupMemberCandidateItem[]) => {
      const results = await runSequentialMembershipAdd(candidates, (candidate) =>
        addAdGroupMember(groupId, {
          memberDistinguishedName: candidate.distinguishedName,
          memberType: normalizeCandidateType(candidate.type),
        }),
      );
      return partitionSequentialAddResults(results);
    },
    onSuccess: async ({ results, succeeded, failed }) => {
      notifySequentialAddResults({
        t,
        results,
        allSuccessMessageKey: "adManagement:membershipMultiSelect.allMembersAdded",
        partialSuccessMessageKey: "adManagement:membershipMultiSelect.partialSuccess",
        allFailedMessageKey: "adManagement:groups.members.addError",
        getDefaultErrorMessage: () => t("adManagement:groups.members.addError"),
      });

      setSelectedCandidates((current) =>
        current.filter((candidate) =>
          failed.some((item) => item.distinguishedName === candidate.distinguishedName),
        ),
      );

      if (succeeded.length > 0) {
        await invalidateAdGroupMemberQueries(queryClient, groupId);
      }

      if (failed.length === 0) {
        handleOpenChange(false);
      }
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:groups.members.addError")),
      );
    },
  });

  const candidates = candidatesQuery.data?.items ?? [];

  function handleSelectCandidate(candidate: AdGroupMemberCandidateItem) {
    if (candidate.isAlreadyDirectMember || selectedDns.has(candidate.distinguishedName)) {
      return;
    }

    setSelectedCandidates((current) => [...current, candidate]);
  }

  return (
    <Dialog open={open}>
      <DialogContent className="max-w-2xl" onOpenChange={handleOpenChange}>
        <DialogHeader>
          <DialogTitle>{t("adManagement:groups.members.addTitle")}</DialogTitle>
          <DialogDescription>{t("adManagement:groups.members.addDescription")}</DialogDescription>
        </DialogHeader>

        <DialogBody>
          <div className="grid gap-4 md:grid-cols-[1fr_180px]">
            <div className="space-y-2">
              <Label htmlFor="group-member-search">
                {t("adManagement:groups.members.searchCandidatesPlaceholder")}
              </Label>
              <Input
                id="group-member-search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t("adManagement:groups.members.searchCandidatesPlaceholder")}
                disabled={addMutation.isPending}
                autoFocus
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="group-member-type-filter">
                {t("adManagement:groups.members.typeFilter")}
              </Label>
              <Select
                id="group-member-type-filter"
                value={typeFilter}
                onChange={(event) => {
                  setTypeFilter(event.target.value as "all" | AdGroupMemberCandidateType);
                }}
                disabled={addMutation.isPending}
              >
                <option value="all">{t("adManagement:groups.members.types.all")}</option>
                <option value="user">{t("adManagement:groups.members.types.user")}</option>
                <option value="group">{t("adManagement:groups.members.types.group")}</option>
                <option value="computer">{t("adManagement:groups.members.types.computer")}</option>
              </Select>
            </div>
          </div>

          {search.trim().length > 0 && search.trim().length < MIN_SEARCH_LENGTH ? (
            <p className="text-sm text-muted-foreground">
              {t("adManagement:groups.empty.searchRequired")}
            </p>
          ) : null}

          {candidatesQuery.isLoading ? <LoadingState /> : null}

          {candidatesQuery.isSuccess && search.trim().length >= MIN_SEARCH_LENGTH ? (
            candidates.length === 0 ? (
              <EmptyState title={t("adManagement:groups.members.noCandidates")} />
            ) : (
              <div className="max-h-64 space-y-2 overflow-y-auto rounded-md border p-2">
                {candidates.map((candidate) => {
                  const primaryLabel = getAdGroupMemberPrimaryLabel(candidate);
                  const secondaryLabel = getAdGroupMemberSecondaryLabel(candidate, primaryLabel);
                  const isAlreadyMember = candidate.isAlreadyDirectMember;
                  const isSelected = selectedDns.has(candidate.distinguishedName);
                  const isDisabled = isAlreadyMember || isSelected;

                  return (
                    <button
                      key={candidate.distinguishedName}
                      type="button"
                      className={cn(
                        "w-full rounded-md border px-3 py-2 text-left transition-colors",
                        isDisabled
                          ? "cursor-not-allowed opacity-50"
                          : "hover:bg-muted/30",
                        isSelected && "border-primary bg-muted/40",
                      )}
                      onClick={() => handleSelectCandidate(candidate)}
                      disabled={isDisabled || addMutation.isPending}
                    >
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium">{primaryLabel}</p>
                        <Badge variant="outline">
                          {getAdGroupMemberTypeLabel(t, candidate.type)}
                        </Badge>
                      </div>
                      {secondaryLabel ? (
                        <p className="truncate text-xs text-muted-foreground" title={secondaryLabel}>
                          {secondaryLabel}
                        </p>
                      ) : null}
                      <p
                        className="mt-1 truncate font-mono text-xs text-muted-foreground"
                        title={candidate.distinguishedName}
                      >
                        {candidate.distinguishedName}
                      </p>
                      {isAlreadyMember ? (
                        <p className="mt-1 text-xs text-muted-foreground">
                          {t("adManagement:membershipMultiSelect.alreadyDirectMember")}
                        </p>
                      ) : null}
                    </button>
                  );
                })}
              </div>
            )
          ) : null}

          <AdMembershipSelectionChips
            title={t("adManagement:membershipMultiSelect.selectedMembers")}
            emptyMessage={t("adManagement:membershipMultiSelect.noMembersSelected")}
            items={selectedCandidates.map((candidate) => {
              const primaryLabel = getAdGroupMemberPrimaryLabel(candidate);
              return {
                key: candidate.distinguishedName,
                primaryLabel,
                secondaryLabel: getAdGroupMemberSecondaryLabel(candidate, primaryLabel),
                distinguishedName: candidate.distinguishedName,
              };
            })}
            onRemove={(key) => {
              setSelectedCandidates((current) =>
                current.filter((candidate) => candidate.distinguishedName !== key),
              );
            }}
            disabled={addMutation.isPending}
            removeAriaLabel={t("adManagement:membershipMultiSelect.removeSelection")}
          />
        </DialogBody>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={addMutation.isPending}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            type="button"
            onClick={() => {
              if (selectedCandidates.length === 0) {
                return;
              }
              addMutation.mutate(selectedCandidates);
            }}
            disabled={selectedCandidates.length === 0 || addMutation.isPending}
          >
            {t("adManagement:membershipMultiSelect.addSelected")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function normalizeCandidateType(
  type: AdGroupMemberCandidateItem["type"],
): AdGroupMemberCandidateType {
  switch (type) {
    case "Group":
      return "group";
    case "Computer":
      return "computer";
    default:
      return "user";
  }
}
