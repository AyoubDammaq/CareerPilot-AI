export interface LoginRequest {
  password: string;
}

export interface LoginResponse {
  token: string;
  /** ISO datetime */
  expiresAt: string;
}
