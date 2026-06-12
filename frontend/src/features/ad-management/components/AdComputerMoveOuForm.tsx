import { useTranslation } from "react-i18next";

import { FormError } from "@/components/common/FormError";
import { AdOuSearchCombobox } from "@/features/ad-management/components/AdOuSearchCombobox";

type Props = {
  targetOuDistinguishedName: string | null;
  onTargetOuChange: (value: string) => void;
  disabled?: boolean;
  sameOuWarning: boolean;
};

export function AdComputerMoveOuForm({
  targetOuDistinguishedName,
  onTargetOuChange,
  disabled,
  sameOuWarning,
}: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <div className="space-y-3">
      <AdOuSearchCombobox
        value={targetOuDistinguishedName}
        onChange={onTargetOuChange}
        disabled={disabled}
        searchContext="computers"
        fieldLabelKey="adManagement:computers.moveOu.targetOu"
        placeholderKey="adManagement:computers.moveOu.targetOuPlaceholder"
        searchKey="adManagement:computers.moveOu.targetOuSearch"
        emptyKey="adManagement:computers.moveOu.targetOuEmpty"
        errorKey="adManagement:computers.moveOu.targetOuLoadFailed"
      />
      {sameOuWarning ? (
        <FormError message={t("computers.moveOu.sameOu")} />
      ) : null}
    </div>
  );
}
