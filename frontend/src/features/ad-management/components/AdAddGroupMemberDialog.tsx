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
  const [selectedCandidate, setSelectedCandidate] = useState<AdGroupMemberCandidateItem | null>(
    null,
  );

  const candidateTypes = useMemo<AdGroupMemberCandidateType[] | undefined>(() => {
    if (typeFilter === "all") {
      return ["user", "group", "computer"];
    }
    return [typeFilter];
  }, [typeFilter]);

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      setSearch("");
      setTypeFilter("all");
      setSelectedCandidate(null);
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
    mutationFn: (candidate: AdGroupMemberCandidateItem) =>
      addAdGroupMember(groupId, {
        memberDistinguishedName: candidate.distinguishedName,
        memberType: normalizeCandidateType(candidate.type),
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(
          response.message || t("adManagement:groups.members.addError"),
        );
        return;
      }

      toast.success(
        response.message || t("adManagement:groups.members.addSuccess"),
      );
      await invalidateAdGroupMemberQueries(queryClient, groupId);
      handleOpenChange(false);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:groups.members.addError")),
      );
    },
  });

  const candidates = candidatesQuery.data?.items ?? [];
  const selectedPrimaryLabel = selectedCandidate
    ? getAdGroupMemberPrimaryLabel(selectedCandidate)
    : null;
  const selectedSecondaryLabel = selectedCandidate && selectedPrimaryLabel
    ? getAdGroupMemberSecondaryLabel(selectedCandidate, selectedPrimaryLabel)
    : null;

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
                onChange={(event) => {
                  setSearch(event.target.value);
                  setSelectedCandidate(null);
                }}
                placeholder={t("adManagement:groups.members.searchCandidatesPlaceholder")}
                disabled={addMutation.isPending}
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
                  setSelectedCandidate(null);
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
                  const isSelected =
                    selectedCandidate?.distinguishedName === candidate.distinguishedName;

                  return (
                    <button
                      key={candidate.distinguishedName}
                      type="button"
                      className={cn(
                        "w-full rounded-md border px-3 py-2 text-left transition-colors hover:bg-muted/30",
                        isSelected && "border-primary bg-muted/40",
                      )}
                      onClick={() => setSelectedCandidate(candidate)}
                      disabled={addMutation.isPending}
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
                    </button>
                  );
                })}
              </div>
            )
          ) : null}

          {selectedCandidate ? (
            <div className="rounded-md border bg-muted/20 p-3 text-sm">
              <p className="text-xs text-muted-foreground">
                {t("adManagement:groups.members.selectCandidate")}
              </p>
              <p className="mt-1 font-medium">{selectedPrimaryLabel}</p>
              {selectedSecondaryLabel ? (
                <p className="truncate text-xs text-muted-foreground" title={selectedSecondaryLabel}>
                  {selectedSecondaryLabel}
                </p>
              ) : null}
              <p
                className="mt-2 break-all font-mono text-xs text-muted-foreground"
                title={selectedCandidate.distinguishedName}
              >
                {selectedCandidate.distinguishedName}
              </p>
            </div>
          ) : null}
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
              if (!selectedCandidate) {
                return;
              }
              addMutation.mutate(selectedCandidate);
            }}
            disabled={!selectedCandidate || addMutation.isPending}
          >
            {t("adManagement:groups.members.add")}
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
