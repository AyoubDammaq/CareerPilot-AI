export interface DashboardDto {
  /** 0-100 */
  profileCompleteness: number;
  missingItems: string[];
  implementedModules: string[];
}
