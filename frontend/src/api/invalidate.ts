import { useQueryClient, type QueryClient } from "@tanstack/react-query"

import {
  getGetItineraryQueryKey,
  getListItinerariesQueryKey,
} from "./generated/itineraries/itineraries"

/** Invalidate itinerary list + optional detail after mutations that change trip data. */
export function invalidateItineraries(
  queryClient: QueryClient,
  itineraryId?: string,
) {
  void queryClient.invalidateQueries({ queryKey: getListItinerariesQueryKey() })
  if (itineraryId) {
    void queryClient.invalidateQueries({
      queryKey: getGetItineraryQueryKey(itineraryId),
    })
  }
}

export function useInvalidateItineraries() {
  const queryClient = useQueryClient()
  return (itineraryId?: string) => invalidateItineraries(queryClient, itineraryId)
}
