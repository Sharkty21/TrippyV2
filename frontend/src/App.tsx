import { Route, Routes } from "react-router-dom"

import { ChatWidget } from "@/components/chat/ChatWidget"
import { LandingPage } from "@/pages/LandingPage"
import { ItineraryDetailPage } from "@/pages/ItineraryDetailPage"

export function App() {
  return (
    <>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/itineraries/:id" element={<ItineraryDetailPage />} />
      </Routes>
      <ChatWidget />
    </>
  )
}

export default App
