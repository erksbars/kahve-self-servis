export const EMPTY_ID = "00000000-0000-0000-0000-000000000000";
export type AdminCategory = {
  id: string;
  name: string;
  sortOrder: number;
  version: number;
};
export type AdminValue = {
  id: string;
  name: string;
  priceDelta: number;
  available: boolean;
  sortOrder: number;
};
export type AdminGroup = {
  id: string;
  name: string;
  minSelect: number;
  maxSelect: number;
  sortOrder: number;
  values: AdminValue[];
};
export type AdminProduct = {
  imageUrl?: string | null;
  imageAlt?: string | null;
  id: string;
  categoryId: string;
  name: string;
  description: string;
  basePrice: number;
  taxRate: number;
  active: boolean;
  sortOrder: number;
  version: number;
  optionGroups: AdminGroup[];
};
export type BranchSetting = {
  branchId: string;
  productId: string;
  priceOverride: number | null;
  available: boolean;
  version: number;
};
export type OptionSetting = {
  branchId: string;
  optionValueId: string;
  priceOverride: number | null;
  available: boolean;
};
export type Catalog = {
  categories: AdminCategory[];
  products: AdminProduct[];
  branchProducts: BranchSetting[];
  branchOptions: OptionSetting[];
};
export type Hours = { day: number; opens: string; closes: string };
export type AdminBranch = {
  id: string;
  name: string;
  slug: string;
  address: string;
  timeZone: string;
  isAcceptingOrders: boolean;
  isOpenNow: boolean;
  preparationCapacity: number;
  version: number;
  hours: Hours[];
};
export type AdminStaff = {
  id: string;
  email: string;
  role: "Owner" | "Barista";
  active: boolean;
  invited: boolean;
  version: number;
  branchIds: string[];
};
export type Summary = {
  paidOrders: number;
  completedOrders: number;
  revenue: number;
  cancelledOrders: number;
  failedPayments: number;
  averagePreparationMinutes: number | null;
  preparationSamples: number;
  timeZone: string;
  currency: string;
  demo: boolean;
};
