import Axios, { type AxiosRequestConfig } from "axios"

import { API_BASE_URL } from "./config"

export const AXIOS_INSTANCE = Axios.create({
  baseURL: API_BASE_URL,
  headers: { "Content-Type": "application/json" },
})

/**
 * Orval axios mutator — signature must be (config, options?) so Orval
 * generates Axios-style clients (data/params) instead of fetch (body/url).
 */
export const customInstance = <T>(
  config: AxiosRequestConfig,
  options?: AxiosRequestConfig,
): Promise<T> => {
  const source = Axios.CancelToken.source()
  const promise = AXIOS_INSTANCE({
    ...config,
    ...options,
    cancelToken: source.token,
  }).then(({ data }) => data)

  // @ts-expect-error cancel is used by orval react-query
  promise.cancel = () => {
    source.cancel("Query was cancelled")
  }

  return promise
}

export default customInstance
